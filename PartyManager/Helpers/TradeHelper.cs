using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using SettlementAutomationCore;

namespace PartyManager.Helpers
{
    public static class TradeHelper
    {
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

        public static List<TradeOrder> GetPreSellOrders(MobileParty party, Settings settings)
        {
            var orders = new List<TradeOrder>();
            if (settings == null || !settings.PreventHerdingPenalty) return orders;

            AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out int cavalry, out int riding, out int pack, out int livestock,
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

        public static List<TradeOrder> GetMainOrders(MobileParty party, Settlement settlement, InventoryLogic currentLogic, Settings settings)
        {
            var orders = new List<TradeOrder>();
            if (settings == null) return orders;

            if (settings.PreventHerdingPenalty)
            {
                orders.AddRange(GetPreSellOrders(party, settings));
            }

            if (AutomationRegistry.IsTradeOptimizerActive())
            {
                return orders;
            }

            // Auto-Buy Mounts
            if (settings.AutoBuyMounts && Hero.MainHero != null && Hero.MainHero.Gold >= settings.MinGoldForMounts)
            {
                AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out _, out int riding, out _, out _,
                    out _, out _, out _);

                int mountsNeeded = infantry - riding;
                if (mountsNeeded > 0)
                {
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

            // Auto-Buy Food
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
                            foodItems = foodItems.OrderBy(f => f.Price).ToList();
                            int syncedDaysLimit = GetSyncedFoodDaysLimit(settings);
                            bool isSurvivalMode = partySize < settings.MinPartySizeForVariety || Hero.MainHero.Gold < settings.MinGoldForVariety;
                            var playerInventory = currentLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);

                            if (isSurvivalMode)
                            {
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

        public static void SubmitLogisticsGoals(MobileParty party, Settings settings)
        {
            if (settings == null) return;

            if (settings.AutoBuyFood && party != null && Hero.MainHero != null)
            {
                int partySize = party.MemberRoster.TotalManCount;
                if (partySize > 0)
                {
                    int syncedDaysLimit = GetSyncedFoodDaysLimit(settings);
                    bool isSurvivalMode = partySize < settings.MinPartySizeForVariety || Hero.MainHero.Gold < settings.MinGoldForVariety;
                    int targetQuantity;
                    int minGoldReserve;

                    if (isSurvivalMode)
                    {
                        targetQuantity = (int)Math.Ceiling(partySize * syncedDaysLimit / 20.0f);
                        int dailyWage = party.TotalWage;
                        minGoldReserve = Math.Max(1000, dailyWage * 2);
                    }
                    else
                    {
                        int minToKeep = (int)Math.Ceiling(partySize * syncedDaysLimit / 200.0f);
                        targetQuantity = 10 * minToKeep;
                        minGoldReserve = settings.MinGoldForVariety;
                    }

                    AutomationRegistry.RegisterLogisticsGoal(new LogisticsGoal(
                        LogisticsGoalType.FoodRestock,
                        targetQuantity,
                        minGoldReserve,
                        isSurvivalMode
                    ));
                }
            }

            if (settings.AutoBuyMounts && Hero.MainHero != null)
            {
                AnimalCalculator.CalculatePartyAnimals(party, out int infantry, out _, out _, out _, out _, out _, out _, out _);
                if (infantry > 0)
                {
                    AutomationRegistry.RegisterLogisticsGoal(new LogisticsGoal(
                        LogisticsGoalType.SpeedMounts,
                        infantry,
                        settings.MinGoldForMounts
                    ));
                }
            }
        }

        private static int GetSyncedFoodDaysLimit(Settings settings)
        {
            int limit = settings?.PartyFoodDaysToKeep ?? 10;
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "TradeOptimizer")
                    {
                        var settingsType = assembly.GetType("TradeOptimizer.Settings");
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
    }
}
