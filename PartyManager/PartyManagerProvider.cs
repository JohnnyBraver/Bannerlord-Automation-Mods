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
    public class PartyManagerProvider : ITradeOrderProvider, IRecruitOrderProvider, IGarrisonOrderProvider, IRansomOrderProvider, IDungeonOrderProvider
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
                bool slaughter = settings.SlaughterAnimalsForHerding;

                // 1. Process Livestock first
                foreach (var el in livestockItems)
                {
                    if (excess <= 0) break;
                    int toProcess = Math.Min(excess, el.Amount);
                    orders.Add(new TradeOrder(el.EquipmentElement, toProcess, false, slaughter));
                    excess -= toProcess;
                }

                // 2. Process Riding Mounts (respecting SellRidingMountsSetting)
                var ridingMode = settings.SellRidingMountsSetting;
                if (excess > 0 && ridingMode != SellRidingMountsMode.Never)
                {
                    int excessRiding = 0;
                    if (ridingMode == SellRidingMountsMode.ExcessOnly)
                    {
                        excessRiding = Math.Max(0, riding - infantry);
                    }
                    else if (ridingMode == SellRidingMountsMode.All)
                    {
                        excessRiding = riding;
                    }

                    if (excessRiding > 0)
                    {
                        foreach (var el in ridingItems)
                        {
                            if (excess <= 0 || excessRiding <= 0) break;
                            int available = el.Amount;
                            int toSell = Math.Min(Math.Min(excess, excessRiding), available);
                            // Ridable mounts are NEVER slaughtered (always sold)
                            orders.Add(new TradeOrder(el.EquipmentElement, toSell, false, false));
                            excess -= toSell;
                            excessRiding -= toSell;
                        }
                    }
                }

                // 3. Process Pack Animals if still over herding limit
                if (excess > 0)
                {
                    foreach (var el in packItems)
                    {
                        if (excess <= 0) break;
                        int toProcess = Math.Min(excess, el.Amount);
                        orders.Add(new TradeOrder(el.EquipmentElement, toProcess, false, slaughter));
                        excess -= toProcess;
                    }
                }
            }

            return orders;
        }

        private int GetSyncedFoodDaysLimit()
        {
            var settings = Settings.Instance;
            int limit = settings?.PartyFoodDaysToKeep ?? 10;
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "TradingOptimizer")
                    {
                        var settingsType = assembly.GetType("TradingOptimizer.Settings");
                        if (settingsType != null)
                        {
                            var instanceProp = settingsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            var settingsInstance = instanceProp?.GetValue(null);
                            if (settingsInstance != null)
                            {
                                var limitProp = settingsType.GetProperty("PartyFoodDaysToKeep", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                if (limitProp != null)
                                {
                                    int toLimit = (int)limitProp.GetValue(settingsInstance);
                                    return Math.Max(limit, toLimit);
                                }
                            }
                        }
                        break;
                    }
                }
            }
            catch {}
            return limit;
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
            if (settings.AutoBuyMounts && Hero.MainHero != null && Hero.MainHero.Gold >= settings.MinGoldForMounts)
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

                    int budget = Hero.MainHero.Gold - settings.MinGoldForMounts;
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

            // Auto-Buy Food (Phase 3)
            if (settings.AutoBuyFood && party != null && Hero.MainHero != null)
            {
                int partySize = party.MemberRoster.TotalManCount;
                if (partySize > 0)
                {
                    int dailyWage = party.TotalWage;
                    int minGoldReserve = Math.Max(1000, dailyWage * 2);
                    int foodBudget = Hero.MainHero.Gold - minGoldReserve;

                    if (foodBudget > 0)
                    {
                        var otherElements = currentLogic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory);
                        var foodItems = new List<RosterElementInfo>();

                        for (int i = 0; i < otherElements.Count; i++)
                        {
                            var el = otherElements[i];
                            if (el.IsEmpty || el.Amount <= 0) continue;
                            var item = el.EquipmentElement.Item;
                            if (item != null && item.IsFood)
                            {
                                int price = currentLogic.GetItemPrice(el.EquipmentElement, true);
                                foodItems.Add(new RosterElementInfo(el.EquipmentElement, el.Amount, price));
                            }
                        }

                        if (foodItems.Count > 0)
                        {
                            // Sort by price ascending to buy cheapest first
                            foodItems = foodItems.OrderBy(f => f.Price).ToList();

                            int syncedDaysLimit = GetSyncedFoodDaysLimit();
                            bool isSurvivalMode = partySize < settings.MinPartySizeForVariety || Hero.MainHero.Gold < settings.MinGoldForVariety;

                            var playerInventory = currentLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);

                            if (isSurvivalMode)
                            {
                                // Survival Mode: Buy cheapest food to meet total food target
                                int totalFoodTarget = (int)Math.Ceiling(partySize * syncedDaysLimit / 20.0f);
                                int totalOwned = 0;
                                for (int j = 0; j < playerInventory.Count; j++)
                                {
                                    var pEl = playerInventory[j];
                                    if (pEl.EquipmentElement.Item != null && pEl.EquipmentElement.Item.IsFood)
                                    {
                                        totalOwned += pEl.Amount;
                                    }
                                }

                                int totalNeeded = totalFoodTarget - totalOwned;
                                if (totalNeeded > 0)
                                {
                                    foreach (var food in foodItems)
                                    {
                                        if (totalNeeded <= 0 || foodBudget <= 0) break;

                                        int toBuy = Math.Min(totalNeeded, food.Amount);
                                        int cost = toBuy * food.Price;
                                        if (cost > foodBudget)
                                        {
                                            toBuy = foodBudget / food.Price;
                                        }

                                        if (toBuy > 0)
                                        {
                                            orders.Add(new TradeOrder(food.EqElement, toBuy, true));
                                            totalNeeded -= toBuy;
                                            foodBudget -= toBuy * food.Price;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Variety Mode: Buy up to minToKeep of each food type
                                int minToKeep = (int)Math.Ceiling(partySize * syncedDaysLimit / 200.0f);

                                foreach (var food in foodItems)
                                {
                                    if (foodBudget <= 0) break;

                                    var itemObj = food.EqElement.Item;
                                    int owned = 0;
                                    for (int j = 0; j < playerInventory.Count; j++)
                                    {
                                        var pEl = playerInventory[j];
                                        if (pEl.EquipmentElement.Item != null && pEl.EquipmentElement.Item.StringId == itemObj.StringId)
                                        {
                                            owned += pEl.Amount;
                                        }
                                    }

                                    int needed = minToKeep - owned;
                                    if (needed > 0)
                                    {
                                        int toBuy = Math.Min(needed, food.Amount);
                                        int cost = toBuy * food.Price;
                                        if (cost > foodBudget)
                                        {
                                            toBuy = foodBudget / food.Price;
                                        }

                                        if (toBuy > 0)
                                        {
                                            orders.Add(new TradeOrder(food.EqElement, toBuy, true));
                                            foodBudget -= toBuy * food.Price;
                                        }
                                    }
                                }
                            }
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
            var leafTroops = new List<CharacterObject>();
            SettlementAutomationCore.Helpers.TroopHelper.GetLeafTroops(troop, leafTroops);

            string logPrefix = $"[Recruit Filter: {troop.Name} (Tier {troop.Tier})]";
            var logLines = new List<string>();

            // Tier evaluation (final leaf evaluation or purchase evaluation)
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
                    // Require at least one final upgrade leaf to match all filters (noble/regular, tier, mounted/foot, archetype)
                    bool leafMatched = false;
                    foreach (var leaf in leafTroops)
                    {
                        string leafInfo = $"Leaf {leaf.Name} (Tier {leaf.Tier})";
                        
                        // Noble / Regular Troop Class check for leaf
                        bool leafIsNoble = leaf.Tier >= 6;
                        if (leafIsNoble && !settings.RecruitNobleSetting)
                        {
                            logLines.Add($"{leafInfo} failed: Noble class, but RecruitNoble is disabled.");
                            continue;
                        }
                        if (!leafIsNoble && !settings.RecruitRegularSetting)
                        {
                            logLines.Add($"{leafInfo} failed: Regular class, but RecruitRegular is disabled.");
                            continue;
                        }

                        if (leaf.Tier < settings.MinRecruitTier || leaf.Tier > settings.MaxRecruitTier)
                        {
                            logLines.Add($"{leafInfo} failed: Tier {leaf.Tier} not in range [{settings.MinRecruitTier}-{settings.MaxRecruitTier}].");
                            continue;
                        }

                        // Combat Mounted / Foot check for leaf
                        bool leafMounted = leaf.IsMounted;
                        if (leafMounted && !settings.RecruitMounted)
                        {
                            logLines.Add($"{leafInfo} failed: Mounted unit, but RecruitMounted is disabled.");
                            continue;
                        }
                        if (!leafMounted && !settings.RecruitFoot)
                        {
                            logLines.Add($"{leafInfo} failed: Foot unit, but RecruitFoot is disabled.");
                            continue;
                        }

                        if (!MatchArchetype(leaf, settings))
                        {
                            logLines.Add($"{leafInfo} failed: Combat archetype mismatch (Crossbow skill={leaf.GetSkillValue(DefaultSkills.Crossbow)}, Bow skill={leaf.GetSkillValue(DefaultSkills.Bow)}, Throwing skill={leaf.GetSkillValue(DefaultSkills.Throwing)}).");
                            continue;
                        }

                        logLines.Add($"{leafInfo} matched all filters.");
                        leafMatched = true;
                        break;
                    }
                    isMatch = leafMatched;
                }
            }
            else
            {
                // Purchase time evaluation
                int maxLeafTier = leafTroops.Count > 0 ? leafTroops.Max(l => l.Tier) : troop.Tier;
                bool isNoble = maxLeafTier >= 6;
                if (isNoble && !settings.RecruitNobleSetting)
                {
                    logLines.Add("Failed: Purchase troop marked as Noble, but RecruitNoble is disabled.");
                    isMatch = false;
                }
                else if (!isNoble && !settings.RecruitRegularSetting)
                {
                    logLines.Add("Failed: Purchase troop marked as Regular, but RecruitRegular is disabled.");
                    isMatch = false;
                }
                else
                {
                    // Combat Mounted / Foot check on purchase troop
                    bool isMounted = troop.IsMounted;
                    if (isMounted && !settings.RecruitMounted)
                    {
                        logLines.Add("Failed: Mounted unit, but RecruitMounted is disabled.");
                        isMatch = false;
                    }
                    else if (!isMounted && !settings.RecruitFoot)
                    {
                        logLines.Add("Failed: Foot unit, but RecruitFoot is disabled.");
                        isMatch = false;
                    }
                    else if (troop.Tier < settings.MinRecruitTier || troop.Tier > settings.MaxRecruitTier)
                    {
                        logLines.Add($"Failed: Tier {troop.Tier} not in range [{settings.MinRecruitTier}-{settings.MaxRecruitTier}].");
                        isMatch = false;
                    }
                    else if (!MatchArchetype(troop, settings))
                    {
                        logLines.Add("Failed: Combat archetype mismatch.");
                        isMatch = false;
                    }
                    else
                    {
                        logLines.Add("Matched all filters.");
                        isMatch = true;
                    }
                }
            }

            SettlementAutomationCore.Helpers.Logger.WriteLog("PartyManager", $"{logPrefix} Result: {isMatch}. Details: {string.Join(" | ", logLines)}");
            return isMatch;
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
            var orders = new List<RansomOrder>();
            if (!settlement.IsTown) return orders;

            var settings = Settings.Instance;
            if (settings == null || !settings.AutoRansomPrisoners) return orders;

            var prisonRoster = party.PrisonRoster;
            for (int i = 0; i < prisonRoster.Count; i++)
            {
                var el = prisonRoster.GetElementCopyAtIndex(i);
                var prisoner = el.Character;
                if (prisoner == null || el.Number <= 0) continue;

                // 1. Keep Heroes check
                if (prisoner.IsHero && settings.KeepHeroPrisoners) continue;

                // 2. Keep Filter check
                if (MatchKeepFilter(prisoner, settings)) continue;

                // 3. Min Tier to ransom check
                if (prisoner.Tier < settings.MinRansomTier) continue;

                orders.Add(new RansomOrder(prisoner, el.Number));
            }

            if (orders.Count > 0)
            {
                var summary = string.Join(", ", orders.Select(o => $"{o.Amount}x {o.Prisoner.Name}"));
                SettlementAutomationCore.Helpers.Logger.WriteLog("PartyManager", $"Prisoner Ransom Orders compiled for {settlement.Name}: {summary}");
            }

            return orders;
        }

        private bool MatchKeepFilter(CharacterObject prisoner, Settings settings)
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

                // Step 1: Check keeping policies by category
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

                // Step 2: Check recruitment filter if policy is KeepSelected
                if (needsRecruitFilterCheck)
                {
                    if (!MatchTroopFilter(evalTroop, settings)) continue;
                }

                // Step 3: Check tier limits
                if (!(evalTroopIsNoble && settings.BypassNoblePrisonerTierLimit))
                {
                    if (prisoner.Tier < settings.MinPrisonerTierToKeep) continue;
                }

                // If any path matches the filters, keep this prisoner type
                return true;
            }

            return false;
        }

        // ----------------------------------------------------
        // IDungeonOrderProvider
        // ----------------------------------------------------
        public List<DungeonOrder> GetDungeonOrders(MobileParty party, Settlement settlement)
        {
            var orders = new List<DungeonOrder>();
            if (!settlement.IsTown && !settlement.IsCastle) return orders;

            var settings = Settings.Instance;
            if (settings == null || !settings.AutoDonatePrisoners) return orders;

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

                // Match keep filter first: if we want to KEEP them, do NOT donate them
                if (MatchKeepFilter(prisoner, settings)) continue;

                if (prisoner.Tier >= settings.MinDonateTier)
                {
                    candidates.Add(new PrisonerInfo(prisoner, el.Number));
                }
            }

            // Sort candidates: high-tier first or standard
            if (settings.PrioritizeHighTierDonation)
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
                SettlementAutomationCore.Helpers.Logger.WriteLog("PartyManager", $"Prisoner Dungeon Donation Orders compiled for {settlement.Name}: {summary}");
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
