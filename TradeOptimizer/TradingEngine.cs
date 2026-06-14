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

namespace TradeOptimizer
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

    public class TradedItemInfo
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public int Gold { get; set; }
        public float MarketPrice { get; set; } // 0 if not known
    }

    public class TradeTransactionReport
    {
        public List<TradedItemInfo> SoldItems { get; } = new List<TradedItemInfo>();
        public List<TradedItemInfo> BoughtItems { get; } = new List<TradedItemInfo>();
        public HashSet<string> SoldNormalItems { get; } = new HashSet<string>();
        public List<(EquipmentElement EqElement, int Amount)> ArbitrageSlaughters { get; } = new List<(EquipmentElement, int)>();
    }

    public static class TradingEngine
    {
        // TODO: Re-implement slaughter arbitrage for livestock purchases (buy and slaughter for profit)
        public static void WriteLog(string message)
        {
            SettlementAutomationCore.Helpers.Logger.WriteLog("TradeOptimizer", message);
        }

        public static TradeTransactionReport RunOptimization(
            SPInventoryVM vm,
            bool isSellPhase,
            bool isBuyPhase,
            SettlementAutomationCore.TradeContext tradeContext,
            HashSet<string>? excludedItems = null)
        {
            var report = new TradeTransactionReport();
            if (vm == null) return report;
            if (tradeContext == null) throw new ArgumentNullException(nameof(tradeContext));

            var settings = Settings.Instance;
            if (settings == null) return report;

            var logic = vm.GetInventoryLogic();
            string otherPartyName = logic?.OtherParty?.Name?.ToString() ?? "Unknown";
            WriteLog($"=== Optimization Run started for: {otherPartyName} (Simulation Mode: {settings.SimulationMode}) ===");

            int partySize = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 1;
            float netWeightAdded = 0f;

            // The Core-owned context has already accounted for reserves and cargo policy.
            int currentBalance = tradeContext.AvailableGold;
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

                    if (!TradeCandidatePolicy.CanTradeByMode(
                            itemObj,
                            settings.FoodTradingMode,
                            settings.LivestockTradingMode,
                            settings.MountsTradingMode,
                            false))
                    {
                        continue;
                    }

                    // Reservations are enforced by Core through the TradeContext sellable snapshot.
                    var sellableEntry = tradeContext.SellableItems.FirstOrDefault(s => s.EquipmentElement.Item?.StringId == itemObj.StringId);
                    int maxSellable = sellableEntry?.AvailableQuantity ?? 0;
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
                                baseReferencePrice = PricingService.GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement);
                            }
                            else if (refMode == PricingReferenceMode.AlwaysLocal)
                            {
                                baseReferencePrice = PricingService.GetLocalCategoryAveragePrice(logic, item.ItemRosterElement.EquipmentElement);
                            }
                            else // PerkBased
                            {
                                if (hasTier1Perks && costBasis > 0)
                                {
                                    baseReferencePrice = costBasis;
                                }
                                else if (hasTier1Perks)
                                {
                                    baseReferencePrice = PricingService.GetTrackedAveragePrice(item.ItemRosterElement);
                                }

                                if (baseReferencePrice <= 0f)
                                {
                                    if (hasTier1Perks && hasTier2Perks)
                                    {
                                        baseReferencePrice = PricingService.GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement);
                                    }
                                    else
                                    {
                                        baseReferencePrice = PricingService.GetLocalCategoryAveragePrice(logic, item.ItemRosterElement.EquipmentElement);
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
                                avgPriceForBuy = PricingService.GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement);
                            }
                            else if (refMode == PricingReferenceMode.AlwaysLocal)
                            {
                                avgPriceForBuy = PricingService.GetLocalCategoryAveragePrice(logic, item.ItemRosterElement.EquipmentElement);
                            }
                            else // PerkBased
                            {
                                if (hasTier1Perks && hasTier2Perks)
                                {
                                    avgPriceForBuy = PricingService.GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement);
                                }
                                else
                                {
                                    avgPriceForBuy = PricingService.GetLocalCategoryAveragePrice(logic, item.ItemRosterElement.EquipmentElement);
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
                                    float currentWeight = SettlementAutomationCore.Helpers.InventoryHelper.GetRosterWeight(MobileParty.MainParty?.ItemRoster) + netWeightAdded;
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
                        report.SoldItems.Add(new TradedItemInfo
                        {
                            Name = itemObj.Name.ToString(),
                            Count = sold,
                            Gold = totalGoldGained,
                            MarketPrice = PricingService.GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement)
                        });
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

                // (Slaughter Arbitrage Sub-Phase removed to simplify purchase loop)
                // Logistics goals (food restocking, speed mounts) are now handled by the Core's AutomationRequest
                // pipeline. The free-trade phase only maximizes profit arbitrage.

                while (true)
                {
                    SPItemVM? bestItem = null;
                    float bestProfitDensity = -1f;

                    foreach (var item in merchantItems)
                    {
                        if (item == null || item.ItemRosterElement.EquipmentElement.Item == null) continue;

                        var itemObj = item.ItemRosterElement.EquipmentElement.Item;

                        // Equipment upgrades are owned by EquipmentManager. TradeOptimizer only buys commodities.
                        if (!TradeCandidatePolicy.CanTradeByMode(
                                itemObj,
                                settings.FoodTradingMode,
                                settings.LivestockTradingMode,
                                settings.MountsTradingMode,
                                true))
                        {
                            continue;
                        }

                        int boughtSoFar = boughtQuantities[item];
                        int initialMerchantCount = item.ItemCount;

                        if (boughtSoFar >= initialMerchantCount) continue;

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

                        if (skipReason == "" && MobileParty.MainParty != null)
                        {
                            if (tradeContext.EnforceCargoLimit)
                            {
                                float freeCargo = tradeContext.FreeCargoCapacity - netWeightAdded;
                                if (freeCargo < itemWeight)
                                {
                                    skipReason = "Overburdened";
                                }
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
                        if (skipReason == "" && currentBalance < currentPrice)
                        {
                            skipReason = $"BudgetProtectionActive (available={currentBalance}, price={currentPrice})";
                        }
                        if (skipReason == "" && itemObj.Name != null && localExcludedItems.Contains(itemObj.Name.ToString()))
                        {
                            skipReason = "SoldInSameStop";
                        }

                        float avgPrice = 0f;
                        if (skipReason == "")
                        {
                            var refMode = settings.PricingReference;
                            if (refMode == PricingReferenceMode.AlwaysGlobal)
                            {
                                avgPrice = PricingService.GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement);
                            }
                            else if (refMode == PricingReferenceMode.AlwaysLocal)
                            {
                                avgPrice = PricingService.GetLocalCategoryAveragePrice(logic, item.ItemRosterElement.EquipmentElement);
                            }
                            else // PerkBased
                            {
                                if (hasTier1Perks && hasTier2Perks)
                                {
                                    avgPrice = PricingService.GetWorldAveragePrice(item.ItemRosterElement.EquipmentElement);
                                }
                                else
                                {
                                    avgPrice = PricingService.GetLocalCategoryAveragePrice(logic, item.ItemRosterElement.EquipmentElement);
                                }
                            }
                        }

                        if (skipReason == "" && avgPrice <= 0f)
                        {
                            skipReason = "AveragePriceUndetermined";
                        }
                        if (skipReason == "" && !TradeCandidatePolicy.PassesBuyPriceThreshold(currentPrice, avgPrice, settings.BuyPriceThresholdFactor))
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

                    if (bestItem == null) break;

                    var bestItemObj = bestItem.ItemRosterElement.EquipmentElement.Item;
                    int price = 0;
                    if (logic != null)
                    {
                        price = logic.GetItemPrice(bestItem.ItemRosterElement.EquipmentElement, true);
                    }
                    else
                    {
                        price = bestItem.ItemCost;
                    }

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

                    boughtQuantities[bestItem] = (boughtQuantities.TryGetValue(bestItem, out int bq) ? bq : 0) + 1;
                    totalGoldSpentMap[bestItem] = (totalGoldSpentMap.TryGetValue(bestItem, out int tg) ? tg : 0) + price;
                    currentBalance -= price;
                    netWeightAdded += bestItemObj.Weight;

                    float dbgAvg = 0f;
                    if (settings.PricingReference == PricingReferenceMode.AlwaysGlobal)
                    {
                        dbgAvg = PricingService.GetWorldAveragePrice(bestItem.ItemRosterElement.EquipmentElement);
                    }
                    else if (settings.PricingReference == PricingReferenceMode.AlwaysLocal)
                    {
                        if (logic != null)
                        {
                            dbgAvg = PricingService.GetLocalCategoryAveragePrice(logic, bestItem.ItemRosterElement.EquipmentElement);
                        }
                    }
                    else
                    {
                        if (Hero.MainHero != null && Hero.MainHero.GetPerkValue(DefaultPerks.Trade.WholeSeller))
                        {
                            dbgAvg = PricingService.GetWorldAveragePrice(bestItem.ItemRosterElement.EquipmentElement);
                        }
                        else
                        {
                            if (logic != null)
                            {
                                dbgAvg = PricingService.GetLocalCategoryAveragePrice(logic, bestItem.ItemRosterElement.EquipmentElement);
                            }
                        }
                    }
                }

                foreach (var pair in boughtQuantities)
                {
                    if (pair.Value > 0)
                    {
                        report.BoughtItems.Add(new TradedItemInfo
                        {
                            Name = pair.Key.ItemRosterElement.EquipmentElement.Item.Name.ToString(),
                            Count = pair.Value,
                            Gold = totalGoldSpentMap[pair.Key],
                            MarketPrice = PricingService.GetWorldAveragePrice(pair.Key.ItemRosterElement.EquipmentElement)
                        });
                    }
                }
            }

            return report;
        }


    }
}
