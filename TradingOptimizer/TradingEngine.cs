using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TradingOptimizer
{
    public static class InventoryVMExtensions
    {
        private static readonly FieldInfo? InventoryLogicField = typeof(SPInventoryVM)
            .GetField("_inventoryLogic", BindingFlags.Instance | BindingFlags.NonPublic);

        public static InventoryLogic? GetInventoryLogic(this SPInventoryVM vm)
        {
            if (vm == null) return null;
            return InventoryLogicField?.GetValue(vm) as InventoryLogic;
        }
    }

    public class TradeTransactionReport
    {
        public List<(string Name, int Count, int Gold)> SoldItems { get; } = new List<(string, int, int)>();
        public List<(string Name, int Count, int Gold)> BoughtItems { get; } = new List<(string, int, int)>();
        public HashSet<string> SoldNormalItems { get; } = new HashSet<string>();
        public List<(EquipmentElement EqElement, int Amount)> ArbitrageSlaughters { get; } = new List<(EquipmentElement, int)>();
    }

    public static class TradingEngine
    {
        private static bool IsSlaughterArbitrageProfitable(InventoryLogic? logic, EquipmentElement eq, out int yieldValue, out int meatCount, out int hideCount)
        {
            yieldValue = 0;
            meatCount = 0;
            hideCount = 0;
            var item = eq.Item;
            if (item == null || item.HorseComponent == null) return false;

            // Never slaughter riding mounts during town entry automation
            bool isRidingMount = item.IsMountable && !item.HorseComponent.IsPackAnimal;
            if (isRidingMount) return false;

            meatCount = item.HorseComponent.MeatCount;
            hideCount = item.HorseComponent.HideCount;
            if (meatCount <= 0 && hideCount <= 0) return false;

            var meatItem = DefaultItems.Meat;
            var hidesItem = DefaultItems.Hides;

            int meatPrice = logic != null ? logic.GetItemPrice(new EquipmentElement(meatItem), false) : meatItem.Value;
            int hidesPrice = logic != null ? logic.GetItemPrice(new EquipmentElement(hidesItem), false) : hidesItem.Value;

            yieldValue = (meatCount * meatPrice) + (hideCount * hidesPrice);
            int buyPrice = logic != null ? logic.GetItemPrice(eq, true) : item.Value;

            return buyPrice < yieldValue;
        }
        public static void WriteLog(string message)
        {
            SettlementAutomationCore.Helpers.Logger.WriteLog("TradingOptimizer", message);
        }

        public static TradeTransactionReport RunOptimization(SPInventoryVM vm, bool isSellPhase, bool isBuyPhase, HashSet<string>? excludedItems = null)
        {
            var report = new TradeTransactionReport();
            if (vm == null) return report;

            var settings = Settings.Instance;
            if (settings == null) return report;

            var logic = vm.GetInventoryLogic();
            string otherPartyName = logic?.OtherParty?.Name?.ToString() ?? "Unknown";
            WriteLog($"=== Optimization Run started for: {otherPartyName} (Simulation Mode: {settings.SimulationMode}) ===");

            int partySize = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 1;
            float netWeightAdded = 0f;

            int currentBalance = (Hero.MainHero?.Gold ?? 0) + (logic != null ? logic.TotalAmount : 0);
            WriteLog($"Initial Balance: {currentBalance} (Hero Gold: {Hero.MainHero?.Gold ?? 0}, Logic TotalAmount: {logic?.TotalAmount ?? 0})");

            // Query perks once at the start
            bool hasTier1Perks = false;
            bool hasTier2Perks = false;
            if (Hero.MainHero != null)
            {
                try
                {
                    var defaultPerksType = typeof(PerkObject).Assembly.GetType("TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks");
                    var tradeType = defaultPerksType?.GetNestedType("Trade", BindingFlags.Public | BindingFlags.NonPublic);
                    if (tradeType != null)
                    {
                        var appraiserProp = tradeType.GetProperty("Appraiser", BindingFlags.Public | BindingFlags.Static);
                        var wholesellerProp = tradeType.GetProperty("WholeSeller", BindingFlags.Public | BindingFlags.Static);
                        var caravanProp = tradeType.GetProperty("CaravanMaster", BindingFlags.Public | BindingFlags.Static);
                        var marketProp = tradeType.GetProperty("MarketDealer", BindingFlags.Public | BindingFlags.Static);
                        
                        var appraiserPerk = appraiserProp?.GetValue(null) as PerkObject;
                        var wholesellerPerk = wholesellerProp?.GetValue(null) as PerkObject;
                        var caravanPerk = caravanProp?.GetValue(null) as PerkObject;
                        var marketPerk = marketProp?.GetValue(null) as PerkObject;
                        
                        if (appraiserPerk != null && Hero.MainHero.GetPerkValue(appraiserPerk)) hasTier1Perks = true;
                        if (wholesellerPerk != null && Hero.MainHero.GetPerkValue(wholesellerPerk)) hasTier1Perks = true;
                        if (caravanPerk != null && Hero.MainHero.GetPerkValue(caravanPerk)) hasTier2Perks = true;
                        if (marketPerk != null && Hero.MainHero.GetPerkValue(marketPerk)) hasTier2Perks = true;
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"[Perk Check Error] Failed checking trade perks: {ex.Message}");
                }
            }
            WriteLog($"Trade Perks Status: hasTier1={hasTier1Perks}, hasTier2={hasTier2Perks}");

            var localExcludedItems = new HashSet<string>();
            if (excludedItems != null)
            {
                foreach (var item in excludedItems)
                {
                    localExcludedItems.Add(item);
                }
            }

            // 1. Sell Phase: Sell profitable items from player inventory (RightItemListVM)
            if (isSellPhase && vm.RightItemListVM != null)
            {
                var playerItems = vm.RightItemListVM.ToList();
                foreach (var item in playerItems)
                {
                    if (item == null || item.ItemRosterElement.EquipmentElement.Item == null) continue;

                    var itemObj = item.ItemRosterElement.EquipmentElement.Item;

                    // Only sell trade goods (commodities)
                    if (!itemObj.IsTradeGood) continue;

                    int minToKeep = 0;
                    if (itemObj.IsFood)
                    {
                        minToKeep = (int)Math.Ceiling(partySize * settings.FoodDaysToKeepPerSoldier);
                    }

                    int maxSellable = item.ItemCount - minToKeep;
                    if (maxSellable <= 0) continue;

                    int sold = 0;
                    int totalGoldGained = 0;
                    float itemWeight = itemObj.Weight;

                    bool isLoot = (item.ItemCost <= 0);

                    while (sold < maxSellable)
                    {
                        int currentPrice = logic != null ? logic.GetItemPrice(item.ItemRosterElement.EquipmentElement, false) : 0;
                        bool loopSell = false;

                        if (isLoot && (settings.LootHandling == LootHandlingMode.Liquidate || settings.LootHandling == LootHandlingMode.XPFarm))
                        {
                            loopSell = currentPrice > 0;
                            WriteLog($"[Sell Check Loot] {itemObj.Name}: Price={currentPrice} -> SELL (Loot Handling: {settings.LootHandling})");
                        }
                        else
                        {
                            float costBasis = item.ItemCost;
                            float baseReferencePrice = 0f;
                            var refMode = settings.PricingReference;

                            if (refMode == PricingReferenceMode.AlwaysGlobal)
                            {
                                baseReferencePrice = GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement);
                            }
                            else if (refMode == PricingReferenceMode.AlwaysLocal)
                            {
                                baseReferencePrice = GetLocalCategoryAveragePrice(logic, item.ItemRosterElement.EquipmentElement);
                            }
                            else // PerkBased
                            {
                                if (hasTier1Perks && costBasis > 0)
                                {
                                    baseReferencePrice = costBasis;
                                }
                                else if (hasTier1Perks)
                                {
                                    baseReferencePrice = GetTrackedAveragePrice(item.ItemRosterElement);
                                }

                                if (baseReferencePrice <= 0f)
                                {
                                    if (hasTier1Perks && hasTier2Perks)
                                    {
                                        baseReferencePrice = GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement);
                                    }
                                    else
                                    {
                                        baseReferencePrice = GetLocalCategoryAveragePrice(logic, item.ItemRosterElement.EquipmentElement);
                                    }
                                }
                            }

                            if (baseReferencePrice <= 0f)
                            {
                                WriteLog($"[Sell Check] {itemObj.Name} skipped: Valuation reference could not be determined.");
                                break;
                            }

                            float sellFactor = settings.SellPriceThresholdFactor;
                            loopSell = currentPrice >= baseReferencePrice * sellFactor;

                            float dbgRatio = baseReferencePrice > 0 ? (float)currentPrice / baseReferencePrice : 1f;
                            string decision = loopSell ? "SELL" : "KEEP";
                            WriteLog($"[Sell Check] {itemObj.Name}: Price={currentPrice}, BaseReference={baseReferencePrice:F1} (Ratio={dbgRatio:P1}, Thresh={sellFactor:P1}), ProfitType={item.ProfitType} -> {decision}");
                        }

                        if (!loopSell)
                        {
                            break;
                        }

                        // Conflict resolution check
                        if (!isLoot || settings.LootHandling == LootHandlingMode.Profit)
                        {
                            int buyPrice = logic != null ? logic.GetItemPrice(item.ItemRosterElement.EquipmentElement, true) : currentPrice;
                            float avgPriceForBuy = 0f;
                            var refMode = settings.PricingReference;

                            if (refMode == PricingReferenceMode.AlwaysGlobal)
                            {
                                avgPriceForBuy = GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement);
                            }
                            else if (refMode == PricingReferenceMode.AlwaysLocal)
                            {
                                avgPriceForBuy = GetLocalCategoryAveragePrice(logic, item.ItemRosterElement.EquipmentElement);
                            }
                            else // PerkBased
                            {
                                if (hasTier1Perks && hasTier2Perks)
                                {
                                    avgPriceForBuy = GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement);
                                }
                                else
                                {
                                    avgPriceForBuy = GetLocalCategoryAveragePrice(logic, item.ItemRosterElement.EquipmentElement);
                                }
                            }

                            bool isBuyCandidate = avgPriceForBuy > 0f && buyPrice <= avgPriceForBuy * settings.BuyPriceThresholdFactor;

                            if (isBuyCandidate)
                            {
                                var stance = settings.Stance;
                                if (stance == TradingStance.MaxProfit)
                                {
                                    WriteLog($"[Sell Check Conflict] {itemObj.Name}: Profitable to sell, but price is below buy threshold. Stance: Max Profit -> KEEP & ACCUMULATE.");
                                    break;
                                }
                                else // Balanced
                                {
                                    float capacity = MobileParty.MainParty?.InventoryCapacity ?? 0f;
                                    float currentWeight = GetRosterWeight(MobileParty.MainParty?.ItemRoster) + netWeightAdded;
                                    bool isCargoFull = capacity > 0f && (currentWeight / capacity) >= 0.80f;
                                    if (!isCargoFull)
                                    {
                                        WriteLog($"[Sell Check Conflict] {itemObj.Name}: Profitable to sell, price below buy threshold. Stance: Balanced (Cargo {currentWeight:F0}/{capacity:F0} < 80%) -> KEEP & ACCUMULATE.");
                                        break;
                                    }
                                    else
                                    {
                                        WriteLog($"[Sell Check Conflict] {itemObj.Name}: Profitable to sell, price below buy threshold. Stance: Balanced (Cargo {currentWeight:F0}/{capacity:F0} >= 80%) -> SELL TO FREE SPACE.");
                                    }
                                }
                            }
                        }

                        int price = currentPrice;
                        if (logic != null && Hero.MainHero != null)
                        {
                            var command = TransferCommand.Transfer(
                                1,
                                InventoryLogic.InventorySide.PlayerInventory,
                                InventoryLogic.InventorySide.OtherInventory,
                                new ItemRosterElement(item.ItemRosterElement.EquipmentElement, 1),
                                EquipmentIndex.None,
                                EquipmentIndex.None,
                                Hero.MainHero.CharacterObject
                            );
                            logic.AddTransferCommand(command);
                        }
                        else
                        {
                            item.ExecuteSellSingle();
                        }
                        sold++;
                        totalGoldGained += price;
                        currentBalance += price;
                        netWeightAdded -= itemWeight;

                        // Exclude from buy phase
                        if (!isLoot || settings.LootHandling != LootHandlingMode.XPFarm)
                        {
                            localExcludedItems.Add(itemObj.Name.ToString());
                        }
                    }

                    if (sold > 0)
                    {
                        report.SoldItems.Add((itemObj.Name.ToString(), sold, totalGoldGained));
                        if (!isLoot)
                        {
                            report.SoldNormalItems.Add(itemObj.Name.ToString());
                        }
                    }
                }
            }

            // 2. Buy Phase: Buy underpriced items from merchant inventory (LeftItemListVM)
            if (isBuyPhase && vm.LeftItemListVM != null)
            {
                var merchantItems = vm.LeftItemListVM.ToList();
                var boughtQuantities = new Dictionary<SPItemVM, int>();
                var totalGoldSpentMap = new Dictionary<SPItemVM, int>();

                foreach (var item in merchantItems)
                {
                    if (item != null)
                    {
                        boughtQuantities[item] = 0;
                        totalGoldSpentMap[item] = 0;
                    }
                }

                // 2a. Slaughter Arbitrage Sub-Phase
                foreach (var item in merchantItems)
                {
                    if (item == null || item.ItemRosterElement.EquipmentElement.Item == null) continue;

                    var eq = item.ItemRosterElement.EquipmentElement;
                    if (IsSlaughterArbitrageProfitable(logic, eq, out int yieldValue, out int meatCount, out int hideCount))
                    {
                        int buyPrice = logic != null ? logic.GetItemPrice(eq, true) : eq.Item.Value;
                        int available = item.ItemCount;
                        if (available <= 0) continue;

                        int toArbitrage = 0;
                        int dailyWage = MobileParty.MainParty?.TotalWage ?? 0;
                        int expenseReserve = dailyWage * settings.MinDaysExpensesToKeep;
                        int minRequiredBalance = Math.Max(settings.MinimumGoldReserve, expenseReserve);
                        int budget = currentBalance - minRequiredBalance;

                        for (int i = 0; i < available; i++)
                        {
                            if (budget < buyPrice) break;

                            // Simulating buy
                            if (logic != null && Hero.MainHero != null)
                            {
                                var buyCommand = TransferCommand.Transfer(
                                    1,
                                    InventoryLogic.InventorySide.OtherInventory,
                                    InventoryLogic.InventorySide.PlayerInventory,
                                    new ItemRosterElement(eq, 1),
                                    EquipmentIndex.None,
                                    EquipmentIndex.None,
                                    Hero.MainHero.CharacterObject
                                );
                                logic.AddTransferCommand(buyCommand);

                                // Simulating slaughter
                                var playerRosterEl = new ItemRosterElement(eq, 1);
                                if (logic.CanSlaughterItem(playerRosterEl, InventoryLogic.InventorySide.PlayerInventory))
                                {
                                    logic.SlaughterItem(playerRosterEl);
                                    toArbitrage++;
                                    budget -= buyPrice;
                                    currentBalance -= buyPrice;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }

                        if (toArbitrage > 0)
                        {
                            report.ArbitrageSlaughters.Add((eq, toArbitrage));
                            report.BoughtItems.Add((eq.Item.Name.ToString(), toArbitrage, buyPrice * toArbitrage));
                            WriteLog($"[Slaughter Arbitrage] Bought and slaughtered {toArbitrage}x {eq.Item.Name} (Buy Price: {buyPrice}, Yield Value: {yieldValue})");
                            InformationManager.DisplayMessage(new InformationMessage($"[TradingOptimizer] Slaughter arbitrage: Bought & Slaughtered {toArbitrage}x {eq.Item.Name}"));
                        }
                    }
                }

                while (true)
                {
                    SPItemVM? bestItem = null;
                    float bestProfitDensity = -1f;

                    foreach (var item in merchantItems)
                    {
                        if (item == null || item.ItemRosterElement.EquipmentElement.Item == null) continue;

                        var itemObj = item.ItemRosterElement.EquipmentElement.Item;

                        // Only buy trade goods
                        if (!itemObj.IsTradeGood) continue;

                        // Check Settings filters (Livestock vs Mounts)
                        if (itemObj.IsAnimal && !itemObj.IsMountable && !settings.TradeLivestock) continue;
                        if (itemObj.IsMountable && !settings.TradeMounts) continue;

                        int boughtSoFar = boughtQuantities[item];
                        int initialMerchantCount = item.ItemCount;

                        // Exclude items that were fully bought in slaughter arbitrage
                        int arbitrageCount = report.ArbitrageSlaughters.Where(s => s.EqElement.Item.StringId == itemObj.StringId).Sum(s => s.Amount);
                        if (boughtSoFar + arbitrageCount >= initialMerchantCount) continue;

                        float itemWeight = itemObj.Weight;
                        var playerItem = vm.RightItemListVM?.FirstOrDefault(r => r.ItemRosterElement.EquipmentElement.Item == itemObj);
                        int currentlyOwned = (playerItem != null ? playerItem.ItemCount : 0) + boughtSoFar;
                        int currentPrice = logic != null ? logic.GetItemPrice(item.ItemRosterElement.EquipmentElement, true) : itemObj.Value;

                        string skipReason = "";
                        
                        // Herding Penalty Protection (limit animal purchases based on remaining herding slots)
                        bool isAnimalOrMount = itemObj.IsAnimal || (itemObj.IsMountable && itemObj.HorseComponent != null);
                        if (isAnimalOrMount && MobileParty.MainParty != null)
                        {
                            int totalAnimalsBoughtInSim = boughtQuantities.Where(p => p.Key.ItemRosterElement.EquipmentElement.Item.IsAnimal || (p.Key.ItemRosterElement.EquipmentElement.Item.IsMountable && p.Key.ItemRosterElement.EquipmentElement.Item.HorseComponent != null)).Sum(p => p.Value);
                            int remainingSlots = SettlementAutomationCore.HerdingCalculator.GetRemainingAnimalSlots(MobileParty.MainParty);
                            if (totalAnimalsBoughtInSim >= remainingSlots)
                            {
                                skipReason = "HerdingLimitExceeded";
                            }
                        }

                        if (skipReason == "" && settings.LimitToInventoryCapacity && MobileParty.MainParty != null)
                        {
                            float currentWeight = GetRosterWeight(MobileParty.MainParty.ItemRoster);
                            float projectedWeight = currentWeight + netWeightAdded;
                            if (projectedWeight + itemWeight >= MobileParty.MainParty.InventoryCapacity)
                            {
                                skipReason = "Overburdened";
                            }
                        }
                        if (skipReason == "" && currentlyOwned >= settings.MaxStackSizeToBuy)
                        {
                            skipReason = $"StackLimitExceeded (owned={currentlyOwned}, max={settings.MaxStackSizeToBuy})";
                        }
                        if (skipReason == "" && currentlyOwned * currentPrice >= settings.MaxStackValueToBuy)
                        {
                            skipReason = $"StackValueLimitExceeded (value={currentlyOwned * currentPrice}, max={settings.MaxStackValueToBuy})";
                        }
                        int dailyWage = MobileParty.MainParty?.TotalWage ?? 0;
                        int expenseReserve = dailyWage * settings.MinDaysExpensesToKeep;
                        int minRequiredBalance = Math.Max(settings.MinimumGoldReserve, expenseReserve);
                        if (skipReason == "" && currentBalance - currentPrice < minRequiredBalance)
                        {
                            skipReason = $"BudgetProtectionActive (projectedBalance={currentBalance - currentPrice}, required={minRequiredBalance})";
                        }
                        if (skipReason == "" && localExcludedItems.Contains(itemObj.Name.ToString()))
                        {
                            skipReason = "SoldInSameStop";
                        }

                        float avgPrice = 0f;
                        if (skipReason == "")
                        {
                            var refMode = settings.PricingReference;
                            if (refMode == PricingReferenceMode.AlwaysGlobal)
                            {
                                avgPrice = GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement);
                            }
                            else if (refMode == PricingReferenceMode.AlwaysLocal)
                            {
                                avgPrice = GetLocalCategoryAveragePrice(logic, item.ItemRosterElement.EquipmentElement);
                            }
                            else // PerkBased
                            {
                                if (hasTier1Perks && hasTier2Perks)
                                {
                                    avgPrice = GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement);
                                }
                                else
                                {
                                    avgPrice = GetLocalCategoryAveragePrice(logic, item.ItemRosterElement.EquipmentElement);
                                }
                            }
                        }

                        if (skipReason == "" && avgPrice <= 0f)
                        {
                            skipReason = "AveragePriceUndetermined";
                        }
                        if (skipReason == "" && currentPrice > avgPrice * settings.BuyPriceThresholdFactor)
                        {
                            skipReason = $"PriceCheckFailed (price={currentPrice}, limit={avgPrice * settings.BuyPriceThresholdFactor:F1}, ratio={(float)currentPrice/avgPrice:P1}, threshold={settings.BuyPriceThresholdFactor:P1})";
                        }
                        if (skipReason == "" && avgPrice - currentPrice <= 0f)
                        {
                            skipReason = "NoProfitExpected";
                        }

                        if (skipReason != "")
                        {
                            if (currentPrice < avgPrice)
                            {
                                WriteLog($"[Buy Skip Diagnostic] {itemObj.Name}: {skipReason}");
                            }
                            continue;
                        }

                        float unitProfit = avgPrice - currentPrice;
                        float itemWeightDivisor = itemWeight > 0.01f ? itemWeight : 0.01f;
                        float profitDensity = unitProfit / itemWeightDivisor;

                        if (profitDensity > bestProfitDensity)
                        {
                            bestProfitDensity = profitDensity;
                            bestItem = item;
                        }
                    }

                    if (bestItem == null)
                    {
                        break;
                    }

                    var bestItemObj = bestItem.ItemRosterElement.EquipmentElement.Item;
                    int price = logic != null ? logic.GetItemPrice(bestItem.ItemRosterElement.EquipmentElement, true) : bestItemObj.Value;
                    float itemWeightVal = bestItemObj.Weight;

                    if (logic != null && Hero.MainHero != null)
                    {
                        var command = TransferCommand.Transfer(
                            1,
                            InventoryLogic.InventorySide.OtherInventory,
                            InventoryLogic.InventorySide.PlayerInventory,
                            new ItemRosterElement(bestItem.ItemRosterElement.EquipmentElement, 1),
                            EquipmentIndex.None,
                            EquipmentIndex.None,
                            Hero.MainHero.CharacterObject
                        );
                        logic.AddTransferCommand(command);
                    }
                    else
                    {
                        bestItem.ExecuteBuySingle();
                    }

                    boughtQuantities[bestItem]++;
                    totalGoldSpentMap[bestItem] += price;
                    currentBalance -= price;
                    netWeightAdded += itemWeightVal;

                    float dbgAvg = 0f;
                    var refModeForDbg = settings.PricingReference;
                    if (refModeForDbg == PricingReferenceMode.AlwaysGlobal)
                    {
                        dbgAvg = GetWorldAveragePrice(bestItem.ItemRosterElement.EquipmentElement);
                    }
                    else if (refModeForDbg == PricingReferenceMode.AlwaysLocal)
                    {
                        dbgAvg = GetLocalCategoryAveragePrice(logic, bestItem.ItemRosterElement.EquipmentElement);
                    }
                    else // PerkBased
                    {
                        if (hasTier1Perks && hasTier2Perks)
                        {
                            dbgAvg = GetWorldAveragePrice(bestItem.ItemRosterElement.EquipmentElement);
                        }
                        else
                        {
                            dbgAvg = GetLocalCategoryAveragePrice(logic, bestItem.ItemRosterElement.EquipmentElement);
                        }
                    }
                    if (dbgAvg <= 0f) dbgAvg = bestItemObj.Value;

                    float dbgRatio = dbgAvg > 0 ? (float)price / dbgAvg : 1f;
                    WriteLog($"[Buy Action] {bestItemObj.Name}: Price={price}, ReferenceAvg={dbgAvg:F1} (Ratio={dbgRatio:P1}, Thresh={settings.BuyPriceThresholdFactor:P1}), Density={bestProfitDensity:F2} -> BOUGHT 1");
                }

                foreach (var pair in boughtQuantities)
                {
                    if (pair.Value > 0)
                    {
                        report.BoughtItems.Add((pair.Key.ItemRosterElement.EquipmentElement.Item.Name.ToString(), pair.Value, totalGoldSpentMap[pair.Key]));
                    }
                }
            }

            return report;
        }

        public static float GetWorldAveragePrice(EquipmentElement equipmentElement)
        {
            var towns = Town.AllTowns;
            if (towns == null || towns.Count == 0)
            {
                return equipmentElement.Item.Value;
            }

            float sumPrices = 0f;
            int count = 0;
            foreach (var town in towns)
            {
                if (town != null)
                {
                    sumPrices += town.GetItemPrice(equipmentElement, null, false);
                    count++;
                }
            }
            return count > 0 ? (sumPrices / count) : equipmentElement.Item.Value;
        }

        private static float GetLocalCategoryAveragePrice(InventoryLogic? logic, EquipmentElement eqElement)
        {
            var town = logic?.CurrentSettlementComponent as Town;
            if (town == null)
            {
                var village = logic?.CurrentSettlementComponent as Village;
                if (village != null && village.TradeBound != null)
                {
                    town = village.TradeBound.Town;
                }
            }
            if (town == null && MobileParty.MainParty != null)
            {
                var mainParty = MobileParty.MainParty;
                var nearestTownSettlement = Settlement.All
                    .Where(s => s.IsTown)
                    .OrderBy(s => s.GetPosition2D.DistanceSquared(mainParty.GetPosition2D))
                    .FirstOrDefault();
                town = nearestTownSettlement?.Town;
            }

            if (town != null)
            {
                float deviationRatio = Helpers.TownHelpers.CalculatePriceDeviationRatio(town, eqElement);
                int currentItemPrice = logic != null ? logic.GetItemPrice(eqElement, false) : 0;
                if (currentItemPrice > 0 && Math.Abs(1f + deviationRatio) > 0.01f)
                {
                    return currentItemPrice / (1f + deviationRatio);
                }
            }
            return eqElement.Item.Value;
        }

        private static float GetTrackedAveragePrice(ItemRosterElement rosterElement)
        {
            try
            {
                var behavior = Campaign.Current?.CampaignBehaviorManager?.GetBehaviors<CampaignBehaviorBase>()
                    ?.FirstOrDefault(b => b.GetType().FullName == "TaleWorlds.CampaignSystem.CampaignBehaviors.TradeSkillCampaignBehavior");
                if (behavior != null)
                {
                    var method = behavior.GetType().GetMethod("GetAveragePriceForItem", BindingFlags.Public | BindingFlags.Instance);
                    if (method != null)
                    {
                        return (int)method.Invoke(behavior, new object[] { rosterElement });
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"[Average Price Check Error] {ex.Message}");
            }
            return 0f;
        }

        private static float GetRosterWeight(ItemRoster? roster)
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
    }
}
