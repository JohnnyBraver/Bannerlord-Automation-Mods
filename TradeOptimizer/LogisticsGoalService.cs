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
using SettlementAutomationCore;

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
            var requests = SettlementAutomationCore.AutomationRegistry.ActiveRequests;
            if (requests == null || requests.Count == 0)
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

            var settings = Settings.Instance;
            int threshold = settings?.InventoryReservationPriorityThreshold ?? 30;
            int minToKeep = 0;

            foreach (var req in requests)
            {
                if (req.Priority < threshold) continue;

                if (req.MatchesItem(itemObj))
                {
                    if (req.Type == SettlementAutomationCore.RequestType.ItemCategory && string.Equals(req.TargetId, "Food", StringComparison.OrdinalIgnoreCase))
                    {
                        if (req.Priority >= 50)
                        {
                            // Survival/Stability food request: protect all food if total food <= target
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
                            if (totalFoodInInventory <= req.TargetQuantity)
                            {
                                return 99999; // Keep all
                            }
                        }
                        else
                        {
                            // Variety food request: keep up to targetQuantity / 10 per type
                            int targetPerType = (int)Math.Ceiling(req.TargetQuantity / 10.0f);
                            minToKeep = Math.Max(minToKeep, targetPerType);
                        }
                    }
                    else
                    {
                        // Mounts, Livestock, PackAnimal, or SpecificItem: protect up to TargetQuantity items in total inventory
                        int totalMatchingInInventory = 0;
                        if (logic != null)
                        {
                            var playerItems = logic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
                            foreach (var el in playerItems)
                            {
                                if (el.EquipmentElement.Item != null && req.MatchesItem(el.EquipmentElement.Item))
                                {
                                    totalMatchingInInventory += el.Amount;
                                }
                            }
                        }
                        if (totalMatchingInInventory <= req.TargetQuantity)
                        {
                            return 99999; // Keep all
                        }
                    }
                }
            }

            return minToKeep;
        }

        public static void SatisfyLogisticsGoals(SPInventoryVM vm, InventoryLogic? logic, Dictionary<SPItemVM, int> boughtQuantities, ref int currentBalance, ref float netWeightAdded, TradeTransactionReport report, HashSet<string> localExcludedItems)
        {
            var requests = SettlementAutomationCore.AutomationRegistry.ActiveRequests;
            if (requests == null || requests.Count == 0) return;

            var settings = Settings.Instance;
            if (settings == null) return;

            int dailyWage = MobileParty.MainParty?.TotalWage ?? 0;
            int expenseReserve = dailyWage * settings.MinDaysExpensesToKeep;
            int defaultMinRequiredBalance = Math.Max(settings.MinimumGoldReserve, expenseReserve);

            var merchantItems = vm.LeftItemListVM?.ToList() ?? new List<SPItemVM>();

            // Process requests by priority descending
            var sortedRequests = requests.OrderByDescending(r => r.Priority).ToList();

            foreach (var req in sortedRequests)
            {
                // Only process Item-based requests (troops are handled in the core recruitment phase)
                if (req.Type != SettlementAutomationCore.RequestType.ItemCategory && req.Type != SettlementAutomationCore.RequestType.SpecificItem)
                    continue;

                int minRequiredBalance = Math.Max(defaultMinRequiredBalance, req.MinGoldReserve);

                if (req.Type == SettlementAutomationCore.RequestType.ItemCategory && string.Equals(req.TargetId, "Food", StringComparison.OrdinalIgnoreCase))
                {
                    // Calculate buyable food items from merchant stock
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

                    if (req.Priority >= 50)
                    {
                        // Survival/Stability mode: buy cheapest food to fill total gap
                        int totalFoodOwned = 0;
                        if (vm.RightItemListVM != null)
                        {
                            totalFoodOwned = vm.RightItemListVM
                                .Where(r => r.ItemRosterElement.EquipmentElement.Item != null && r.ItemRosterElement.EquipmentElement.Item.IsFood)
                                .Sum(r => r.ItemCount);
                        }
                        totalFoodOwned += boughtQuantities.Where(q => q.Key.ItemRosterElement.EquipmentElement.Item != null && q.Key.ItemRosterElement.EquipmentElement.Item.IsFood).Sum(q => q.Value);

                        int deficit = req.TargetQuantity - totalFoodOwned;
                        if (deficit <= 0) continue;

                        foreach (var food in buyableFood)
                        {
                            if (deficit <= 0) break;
                            var itemObj = food.ItemRosterElement.EquipmentElement.Item;

                            int bCount = boughtQuantities.TryGetValue(food, out int bVal) ? bVal : 0;
                            int merchantCount = food.ItemCount - bCount;
                            int price = logic != null ? logic.GetItemPrice(food.ItemRosterElement.EquipmentElement, true) : itemObj.Value;

                            // Price multiplier check
                            float baseValue = itemObj.Value;
                            if (baseValue > 0)
                            {
                                float mult = (float)price / baseValue;
                                if (mult > req.MaxPriceMultiplier)
                                {
                                    continue; // skip overpriced
                                }
                            }

                            for (int i = 0; i < merchantCount; i++)
                            {
                                if (deficit <= 0) break;
                                if (currentBalance - price < minRequiredBalance) break;

                                // Inventory capacity limits
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
                                deficit--;
                            }
                        }
                    }
                    else
                    {
                        // Variety food request: buy up to targetQuantity / 10 of each food type
                        int targetPerType = (int)Math.Ceiling(req.TargetQuantity / 10.0f);
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

                            // Price multiplier check
                            float baseValue = itemObj.Value;
                            if (baseValue > 0)
                            {
                                float mult = (float)price / baseValue;
                                if (mult > req.MaxPriceMultiplier)
                                {
                                    continue; // skip overpriced
                                }
                            }

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

                                // Inventory capacity limits
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
                else
                {
                    // Other categories (Horse, PackAnimal, Livestock) or SpecificItem
                    int totalOwned = 0;
                    if (vm.RightItemListVM != null)
                    {
                        totalOwned = vm.RightItemListVM
                            .Where(r => r.ItemRosterElement.EquipmentElement.Item != null && req.MatchesItem(r.ItemRosterElement.EquipmentElement.Item))
                            .Sum(r => r.ItemCount);
                    }
                    totalOwned += boughtQuantities
                        .Where(q => q.Key.ItemRosterElement.EquipmentElement.Item != null && req.MatchesItem(q.Key.ItemRosterElement.EquipmentElement.Item))
                        .Sum(q => q.Value);

                    int needed = req.TargetQuantity - totalOwned;
                    if (needed <= 0) continue;

                    // Gather matching items from merchant stock
                    var buyableItems = new List<SPItemVM>();
                    foreach (var item in merchantItems)
                    {
                        if (item == null || item.ItemRosterElement.EquipmentElement.Item == null) continue;
                        var itemObj = item.ItemRosterElement.EquipmentElement.Item;
                        if (req.MatchesItem(itemObj) && !localExcludedItems.Contains(itemObj.Name.ToString()))
                        {
                            buyableItems.Add(item);
                        }
                    }

                    // Sort by local purchase price ascending
                    buyableItems = buyableItems.OrderBy(f => logic != null ? logic.GetItemPrice(f.ItemRosterElement.EquipmentElement, true) : f.ItemRosterElement.EquipmentElement.Item.Value).ToList();

                    foreach (var itemVM in buyableItems)
                    {
                        if (needed <= 0) break;
                        var itemObj = itemVM.ItemRosterElement.EquipmentElement.Item;

                        int bCount = boughtQuantities.TryGetValue(itemVM, out int bVal) ? bVal : 0;
                        int merchantCount = itemVM.ItemCount - bCount;
                        int price = logic != null ? logic.GetItemPrice(itemVM.ItemRosterElement.EquipmentElement, true) : itemObj.Value;

                        // Price multiplier check
                        float baseValue = itemObj.Value;
                        if (baseValue > 0)
                        {
                            float mult = (float)price / baseValue;
                            if (mult > req.MaxPriceMultiplier)
                            {
                                continue; // skip overpriced
                            }
                        }

                        // Mount specific price throttle
                        if (itemObj.IsMountable && !itemObj.HorseComponent.IsPackAnimal)
                        {
                            float referencePrice = PricingService.GetWorldAveragePrice(itemVM.ItemRosterElement.EquipmentElement);
                            if (referencePrice <= 0f) referencePrice = itemObj.Value;
                            if (price > referencePrice * settings.MountsLogisticsPriceThrottleFactor)
                            {
                                TradingEngine.WriteLog($"[Logistics] Skipping mount purchase of {itemObj.Name}: price {price} is above threshold ({referencePrice * settings.MountsLogisticsPriceThrottleFactor:F1})");
                                continue;
                            }
                        }

                        for (int i = 0; i < merchantCount; i++)
                        {
                            if (needed <= 0) break;
                            if (currentBalance - price < minRequiredBalance) break;

                            // Herding protection limits for horses/livestock
                            if ((itemObj.IsMountable || itemObj.IsAnimal) && MobileParty.MainParty != null)
                            {
                                int totalAnimalsBoughtInSim = boughtQuantities.Where(p => p.Key.ItemRosterElement.EquipmentElement.Item != null && (p.Key.ItemRosterElement.EquipmentElement.Item.IsAnimal || p.Key.ItemRosterElement.EquipmentElement.Item.IsMountable)).Sum(p => p.Value);
                                int remainingSlots = SettlementAutomationCore.HerdingCalculator.GetRemainingAnimalSlots(MobileParty.MainParty);
                                if (totalAnimalsBoughtInSim >= remainingSlots) break;
                            }

                            // Inventory capacity limits
                            if (settings.LimitToInventoryCapacity && MobileParty.MainParty != null)
                            {
                                float projectedWeight = GetRosterWeight(MobileParty.MainParty.ItemRoster) + netWeightAdded;
                                if (projectedWeight + itemObj.Weight >= MobileParty.MainParty.InventoryCapacity) break;
                            }

                            if (logic != null && Hero.MainHero != null)
                            {
                                var command = TransferCommand.Transfer(1, InventoryLogic.InventorySide.OtherInventory, InventoryLogic.InventorySide.PlayerInventory, new ItemRosterElement(itemVM.ItemRosterElement.EquipmentElement, 1), EquipmentIndex.None, EquipmentIndex.None, Hero.MainHero.CharacterObject);
                                logic.AddTransferCommand(command);
                            }

                            boughtQuantities[itemVM] = (boughtQuantities.TryGetValue(itemVM, out int currentBVal) ? currentBVal : 0) + 1;
                            currentBalance -= price;
                            netWeightAdded += itemObj.Weight;
                            report.BoughtItems.Add(new TradedItemInfo
                            {
                                Name = itemObj.Name.ToString(),
                                Count = 1,
                                Gold = price,
                                MarketPrice = PricingService.GetWorldAveragePrice(itemVM.ItemRosterElement.EquipmentElement)
                            });
                            needed--;
                        }
                    }
                }
            }
        }
    }
}
