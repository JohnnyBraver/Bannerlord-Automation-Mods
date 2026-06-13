using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TradeOptimizer
{
    public static class LogisticsGoalService
    {
        public static int GetSyncedFoodDaysLimit()
        {
            var settings = Settings.Instance;
            int limit = settings?.PartyFoodDaysToKeep ?? 10;
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "PartyManager")
                    {
                        var settingsType = assembly.GetType("PartyManager.Settings");
                        if (settingsType != null)
                        {
                            var instanceProp = settingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                            var settingsInstance = instanceProp?.GetValue(null);
                            if (settingsInstance != null)
                            {
                                var limitProp = settingsType.GetProperty("PartyFoodDaysToKeep", BindingFlags.Public | BindingFlags.Instance);
                                if (limitProp != null)
                                {
                                    int pmLimit = (int)limitProp.GetValue(settingsInstance);
                                    return Math.Max(limit, pmLimit);
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

        public static float GetRosterWeight(ItemRoster? roster)
        {
            if (roster == null) return 0f;
            float weight = 0f;
            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (element.EquipmentElement.Item != null)
                {
                    var item = element.EquipmentElement.Item;
                    if (item.IsAnimal || item.IsMountable)
                    {
                        continue;
                    }
                    weight += item.Weight * element.Amount;
                }
            }
            return weight;
        }

        public static int GetMinToKeepForLogistics(ItemObject itemObj, InventoryLogic? logic)
        {
            var goals = SettlementAutomationCore.AutomationRegistry.ActiveLogisticsGoals;
            if (goals == null || goals.Count == 0)
            {
                // Standalone fallback: maintain standard food minToKeep if it's food
                if (itemObj.IsFood)
                {
                    int partySize = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 1;
                    int syncedDaysLimit = GetSyncedFoodDaysLimit();
                    return (int)Math.Ceiling(partySize * syncedDaysLimit / 200.0f);
                }
                return 0;
            }

            int minToKeep = 0;

            if (itemObj.IsFood)
            {
                var foodGoal = goals.FirstOrDefault(g => g.GoalType == SettlementAutomationCore.LogisticsGoalType.FoodRestock);
                if (foodGoal != null)
                {
                    if (foodGoal.IsSurvivalMode)
                    {
                        // In survival mode, we protect a total number of food items in inventory.
                        int totalFoodInInventory = 0;
                        if (logic != null)
                        {
                            var playerItems = logic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
                            foreach (var el in playerItems)
                            {
                                if (el.EquipmentElement.Item != null && el.EquipmentElement.Item.IsFood)
                                {
                                    totalFoodInInventory += el.Amount;
                                }
                            }
                        }
                        if (totalFoodInInventory <= foodGoal.TargetQuantity)
                        {
                            return 99999; // Keep all
                        }
                    }
                    else
                    {
                        // In variety mode, target quantity is divided by 10 (food types) to get minToKeep per type
                        minToKeep = (int)Math.Ceiling(foodGoal.TargetQuantity / 10.0f);
                    }
                }
            }
            else if (itemObj.IsMountable && !itemObj.HorseComponent.IsPackAnimal)
            {
                var mountGoal = goals.FirstOrDefault(g => g.GoalType == SettlementAutomationCore.LogisticsGoalType.SpeedMounts);
                if (mountGoal != null)
                {
                    // For speed mounts, we protect up to TargetQuantity riding horses.
                    int totalMountsInInventory = 0;
                    if (logic != null)
                    {
                        var playerItems = logic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
                        foreach (var el in playerItems)
                        {
                            var item = el.EquipmentElement.Item;
                            if (item != null && item.IsMountable && !item.HorseComponent.IsPackAnimal)
                            {
                                totalMountsInInventory += el.Amount;
                            }
                        }
                    }
                    if (totalMountsInInventory <= mountGoal.TargetQuantity)
                    {
                        return 99999; // Keep all
                    }
                }
            }

            return minToKeep;
        }

        public static void SatisfyLogisticsGoals(SPInventoryVM vm, InventoryLogic? logic, Dictionary<SPItemVM, int> boughtQuantities, ref int currentBalance, ref float netWeightAdded, TradeTransactionReport report, HashSet<string> localExcludedItems)
        {
            var goals = SettlementAutomationCore.AutomationRegistry.ActiveLogisticsGoals;
            if (goals == null || goals.Count == 0) return;

            var settings = Settings.Instance;
            if (settings == null) return;

            int dailyWage = MobileParty.MainParty?.TotalWage ?? 0;
            int expenseReserve = dailyWage * settings.MinDaysExpensesToKeep;
            int defaultMinRequiredBalance = Math.Max(settings.MinimumGoldReserve, expenseReserve);

            var merchantItems = vm.LeftItemListVM?.ToList() ?? new List<SPItemVM>();

            foreach (var goal in goals)
            {
                int minRequiredBalance = Math.Max(defaultMinRequiredBalance, goal.MinGoldReserve);

                if (goal.GoalType == SettlementAutomationCore.LogisticsGoalType.FoodRestock)
                {
                    // Calculate how much food we currently own
                    int totalFoodOwned = 0;
                    if (vm.RightItemListVM != null)
                    {
                        totalFoodOwned = vm.RightItemListVM
                            .Where(r => r.ItemRosterElement.EquipmentElement.Item != null && r.ItemRosterElement.EquipmentElement.Item.IsFood)
                            .Sum(r => r.ItemCount);
                    }
                    totalFoodOwned += boughtQuantities.Where(q => q.Key.ItemRosterElement.EquipmentElement.Item.IsFood).Sum(q => q.Value);

                    int needed = goal.TargetQuantity - totalFoodOwned;
                    if (needed <= 0) continue;

                    // Gather all food items in merchant stock
                    var buyableFood = new List<SPItemVM>();
                    foreach (var item in merchantItems)
                    {
                        if (item == null || item.ItemRosterElement.EquipmentElement.Item == null) continue;
                        var itemObj = item.ItemRosterElement.EquipmentElement.Item;
                        if (itemObj.IsFood && !localExcludedItems.Contains(itemObj.Name.ToString()))
                        {
                            buyableFood.Add(item);
                        }
                    }

                    // Sort food by local price ascending
                    buyableFood = buyableFood.OrderBy(f => logic != null ? logic.GetItemPrice(f.ItemRosterElement.EquipmentElement, true) : f.ItemRosterElement.EquipmentElement.Item.Value).ToList();

                    if (goal.IsSurvivalMode)
                    {
                        // Survival mode: buy cheapest food to fill total gap
                        foreach (var food in buyableFood)
                        {
                            if (needed <= 0) break;
                            var itemObj = food.ItemRosterElement.EquipmentElement.Item;
                            
                            int bCount = boughtQuantities.TryGetValue(food, out int bVal) ? bVal : 0;
                            int merchantCount = food.ItemCount - bCount;
                            int price = logic != null ? logic.GetItemPrice(food.ItemRosterElement.EquipmentElement, true) : itemObj.Value;

                            for (int i = 0; i < merchantCount; i++)
                            {
                                if (needed <= 0) break;
                                if (currentBalance - price < minRequiredBalance) break;
                                if (settings.LimitToInventoryCapacity && MobileParty.MainParty != null)
                                {
                                    float projectedWeight = GetRosterWeight(MobileParty.MainParty.ItemRoster) + netWeightAdded;
                                    if (projectedWeight + itemObj.Weight >= MobileParty.MainParty.InventoryCapacity) break;
                                }

                                if (logic != null && Hero.MainHero != null)
                                {
                                    var command = TransferCommand.Transfer(1, InventoryLogic.InventorySide.OtherInventory, InventoryLogic.InventorySide.PlayerInventory, new ItemRosterElement(food.ItemRosterElement.EquipmentElement, 1), EquipmentIndex.None, EquipmentIndex.None, Hero.MainHero.CharacterObject);
                                    logic.AddTransferCommand(command);
                                }

                                boughtQuantities[food] = (boughtQuantities.TryGetValue(food, out int currentBVal) ? currentBVal : 0) + 1;
                                currentBalance -= price;
                                netWeightAdded += itemObj.Weight;
                                report.BoughtItems.Add(new TradedItemInfo
                                {
                                    Name = itemObj.Name.ToString(),
                                    Count = 1,
                                    Gold = price,
                                    MarketPrice = PricingService.GetWorldAveragePrice(food.ItemRosterElement.EquipmentElement)
                                });
                                needed--;
                            }
                        }
                    }
                    else
                    {
                        // Variety mode: buy up to targetQuantity / 10 of each food type
                        int targetPerType = (int)Math.Ceiling(goal.TargetQuantity / 10.0f);
                        foreach (var food in buyableFood)
                        {
                            var itemObj = food.ItemRosterElement.EquipmentElement.Item;
                            var playerItem = vm.RightItemListVM?.FirstOrDefault(r => r.ItemRosterElement.EquipmentElement.Item == itemObj);
                            
                            int bCount = boughtQuantities.TryGetValue(food, out int bVal) ? bVal : 0;
                            int ownedOfThis = (playerItem != null ? playerItem.ItemCount : 0) + bCount;

                            int typeNeeded = targetPerType - ownedOfThis;
                            if (typeNeeded <= 0) continue;

                            int merchantCount = food.ItemCount - bCount;
                            int price = logic != null ? logic.GetItemPrice(food.ItemRosterElement.EquipmentElement, true) : itemObj.Value;

                            int criticalMin = (int)Math.Ceiling(targetPerType / 10.0f);
                            float referencePrice = PricingService.GetWorldAveragePrice(food.ItemRosterElement.EquipmentElement);
                            if (referencePrice <= 0f) referencePrice = itemObj.Value;

                            for (int i = 0; i < Math.Min(typeNeeded, merchantCount); i++)
                            {
                                int currentlyOwnedDuringLoop = ownedOfThis + i;
                                bool isCritical = settings.ForceBuyMinVarietyFood && (currentlyOwnedDuringLoop < criticalMin);

                                if (!isCritical && price > referencePrice * settings.FoodLogisticsPriceThrottleFactor)
                                {
                                    TradingEngine.WriteLog($"[Logistics] Skipping food variety purchase of {itemObj.Name}: price {price} is above threshold ({referencePrice * settings.FoodLogisticsPriceThrottleFactor:F1})");
                                    break;
                                }

                                if (currentBalance - price < minRequiredBalance) break;
                                if (settings.LimitToInventoryCapacity && MobileParty.MainParty != null)
                                {
                                    float projectedWeight = GetRosterWeight(MobileParty.MainParty.ItemRoster) + netWeightAdded;
                                    if (projectedWeight + itemObj.Weight >= MobileParty.MainParty.InventoryCapacity) break;
                                }

                                if (logic != null && Hero.MainHero != null)
                                {
                                    var command = TransferCommand.Transfer(1, InventoryLogic.InventorySide.OtherInventory, InventoryLogic.InventorySide.PlayerInventory, new ItemRosterElement(food.ItemRosterElement.EquipmentElement, 1), EquipmentIndex.None, EquipmentIndex.None, Hero.MainHero.CharacterObject);
                                    logic.AddTransferCommand(command);
                                }

                                boughtQuantities[food] = (boughtQuantities.TryGetValue(food, out int currentBVal) ? currentBVal : 0) + 1;
                                currentBalance -= price;
                                netWeightAdded += itemObj.Weight;
                                report.BoughtItems.Add(new TradedItemInfo
                                {
                                    Name = itemObj.Name.ToString(),
                                    Count = 1,
                                    Gold = price,
                                    MarketPrice = PricingService.GetWorldAveragePrice(food.ItemRosterElement.EquipmentElement)
                                });
                            }
                        }
                    }
                }
                else if (goal.GoalType == SettlementAutomationCore.LogisticsGoalType.SpeedMounts)
                {
                    // Speed mounts: buy riding mounts up to target quantity
                    int totalMountsOwned = 0;
                    if (vm.RightItemListVM != null)
                    {
                        totalMountsOwned = vm.RightItemListVM
                            .Where(r => r.ItemRosterElement.EquipmentElement.Item != null && r.ItemRosterElement.EquipmentElement.Item.IsMountable && !r.ItemRosterElement.EquipmentElement.Item.HorseComponent.IsPackAnimal)
                            .Sum(r => r.ItemCount);
                    }
                    totalMountsOwned += boughtQuantities.Where(q => q.Key.ItemRosterElement.EquipmentElement.Item.IsMountable && !q.Key.ItemRosterElement.EquipmentElement.Item.HorseComponent.IsPackAnimal).Sum(q => q.Value);

                    int needed = goal.TargetQuantity - totalMountsOwned;
                    if (needed <= 0) continue;

                    var buyableMounts = new List<SPItemVM>();
                    foreach (var item in merchantItems)
                    {
                        if (item == null || item.ItemRosterElement.EquipmentElement.Item == null) continue;
                        var itemObj = item.ItemRosterElement.EquipmentElement.Item;
                        if (itemObj.IsMountable && !itemObj.HorseComponent.IsPackAnimal && !localExcludedItems.Contains(itemObj.Name.ToString()))
                        {
                            buyableMounts.Add(item);
                        }
                    }

                    buyableMounts = buyableMounts.OrderBy(m => logic != null ? logic.GetItemPrice(m.ItemRosterElement.EquipmentElement, true) : m.ItemRosterElement.EquipmentElement.Item.Value).ToList();

                    foreach (var mount in buyableMounts)
                    {
                        if (needed <= 0) break;
                        var itemObj = mount.ItemRosterElement.EquipmentElement.Item;
                        
                        int bCount = boughtQuantities.TryGetValue(mount, out int bVal) ? bVal : 0;
                        int merchantCount = mount.ItemCount - bCount;
                        int price = logic != null ? logic.GetItemPrice(mount.ItemRosterElement.EquipmentElement, true) : itemObj.Value;

                        float referencePrice = PricingService.GetWorldAveragePrice(mount.ItemRosterElement.EquipmentElement);
                        if (referencePrice <= 0f) referencePrice = itemObj.Value;
                        if (price > referencePrice * settings.MountsLogisticsPriceThrottleFactor)
                        {
                            TradingEngine.WriteLog($"[Logistics] Skipping mount purchase of {itemObj.Name}: price {price} is above threshold ({referencePrice * settings.MountsLogisticsPriceThrottleFactor:F1})");
                            continue;
                        }

                        for (int i = 0; i < merchantCount; i++)
                        {
                            if (needed <= 0) break;
                            if (currentBalance - price < minRequiredBalance) break;

                            if (MobileParty.MainParty != null)
                            {
                                int totalAnimalsBoughtInSim = boughtQuantities.Where(p => p.Key.ItemRosterElement.EquipmentElement.Item.IsAnimal || (p.Key.ItemRosterElement.EquipmentElement.Item.IsMountable && p.Key.ItemRosterElement.EquipmentElement.Item.HorseComponent != null)).Sum(p => p.Value);
                                int remainingSlots = SettlementAutomationCore.HerdingCalculator.GetRemainingAnimalSlots(MobileParty.MainParty);
                                if (totalAnimalsBoughtInSim >= remainingSlots) break;
                            }

                            if (settings.LimitToInventoryCapacity && MobileParty.MainParty != null)
                            {
                                float projectedWeight = GetRosterWeight(MobileParty.MainParty.ItemRoster) + netWeightAdded;
                                if (projectedWeight + itemObj.Weight >= MobileParty.MainParty.InventoryCapacity) break;
                            }

                            if (logic != null && Hero.MainHero != null)
                            {
                                var command = TransferCommand.Transfer(1, InventoryLogic.InventorySide.OtherInventory, InventoryLogic.InventorySide.PlayerInventory, new ItemRosterElement(mount.ItemRosterElement.EquipmentElement, 1), EquipmentIndex.None, EquipmentIndex.None, Hero.MainHero.CharacterObject);
                                logic.AddTransferCommand(command);
                            }

                            boughtQuantities[mount] = (boughtQuantities.TryGetValue(mount, out int currentBVal) ? currentBVal : 0) + 1;
                            currentBalance -= price;
                            netWeightAdded += itemObj.Weight;
                            report.BoughtItems.Add(new TradedItemInfo
                            {
                                Name = itemObj.Name.ToString(),
                                Count = 1,
                                Gold = price,
                                MarketPrice = PricingService.GetWorldAveragePrice(mount.ItemRosterElement.EquipmentElement)
                            });
                            needed--;
                        }
                    }
                }
            }
        }
    }
}
