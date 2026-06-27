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
            const int MaxBuyLoopIterations = 1000;
            const int MaxBuySkipDiagnostics = 25;

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

            float startWeight = MobileParty.MainParty != null ? SettlementAutomationCore.Helpers.InventoryHelper.GetRosterWeight(MobileParty.MainParty.ItemRoster) : 0f;
            float usableCapacity = startWeight + tradeContext.CargoCapacityBalance;

            // The Core-owned context has already accounted for reserves and cargo policy.
            int currentBalance = tradeContext.AvailableGold;
            WriteLog($"Initial Balance: {currentBalance} (Hero Gold: {Hero.MainHero?.Gold ?? 0}, Logic TotalAmount: {logic?.TotalAmount ?? 0})");

            WriteLog($"Trade Perks Status: hasTier1={PricingService.HasTier1Perks()}, hasTier2={PricingService.HasTier2Perks()}");

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
                            settings.CraftingMaterialsTradingMode,
                            false))
                    {
                        continue;
                    }

                    // Reservations are enforced by Core through the TradeContext sellable snapshot.
                    var sellableEntry = tradeContext.SellableItems.FirstOrDefault(s => s.Matches(item.ItemRosterElement.EquipmentElement));
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
                        float baseReferencePrice = 0f;

                        if (isLoot && (settings.LootHandling == LootHandlingMode.Liquidate || settings.LootHandling == LootHandlingMode.XPFarm))
                        {
                            loopSell = currentPrice > 0;
                            WriteLog($"[Sell Check Loot] {itemObj.Name}: Price={currentPrice} -> SELL (Loot Handling: {settings.LootHandling})");
                        }
                        else
                        {
                            float costBasis = item.ItemCost;
                            baseReferencePrice = PricingService.GetReferencePrice(
                                logic,
                                item.ItemRosterElement.EquipmentElement,
                                item.ItemRosterElement,
                                costBasis,
                                isSelling: true
                            );

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
                            float avgPriceForBuy = PricingService.GetReferencePrice(
                                logic,
                                item.ItemRosterElement.EquipmentElement,
                                null,
                                0f,
                                isSelling: false
                            );

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
                                    bool isGoodSell = baseReferencePrice > 0f && currentPrice >= baseReferencePrice * settings.GoodSellThreshold;
                                    if (isGoodSell)
                                    {
                                        WriteLog($"[Sell Check Conflict Override] {itemObj.Name}: Price={currentPrice} >= Good Sell Threshold ({baseReferencePrice * settings.GoodSellThreshold:F1}). Override conflict -> SELL.");
                                    }
                                    else
                                    {
                                        float currentWeightNow = startWeight + netWeightAdded;
                                        float fullness = usableCapacity > 0f ? (currentWeightNow / usableCapacity) : 0f;
                                        float limit = settings.CargoLimitThreshold;
                                        bool isCargoFull = fullness >= limit;
                                        if (!isCargoFull)
                                        {
                                            WriteLog($"[Sell Check Conflict] {itemObj.Name}: Profitable to sell, price below buy threshold. Stance: Balanced (Usable Cargo Fullness {fullness:P0} < {limit:P0}) -> KEEP & ACCUMULATE.");
                                            break;
                                        }
                                        else
                                        {
                                            WriteLog($"[Sell Check Conflict] {itemObj.Name}: Profitable to sell, price below buy threshold. Stance: Balanced (Usable Cargo Fullness {fullness:P0} >= {limit:P0}) -> SELL TO FREE SPACE.");
                                        }
                                    }
                                }
                            }
                        }

                        int price = currentPrice;
                        if (!settings.SimulationMode && logic != null && Hero.MainHero != null)
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
                        else if (!settings.SimulationMode)
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
                            localExcludedItems.Add(GetTradeIdentityKey(item.ItemRosterElement.EquipmentElement));
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
                var loggedSkipDiagnostics = new HashSet<string>();
                int buySkipDiagnosticsWritten = 0;
                bool buySkipDiagnosticsSuppressed = false;
                int buyLoopIterations = 0;

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

                var extraSoldQuantities = new Dictionary<string, int>();

                while (true)
                {
                    buyLoopIterations++;
                    if (buyLoopIterations > MaxBuyLoopIterations)
                    {
                        WriteLog($"[Buy Loop Guard] Stopped buy optimization after {MaxBuyLoopIterations} iterations to keep town-entry automation responsive.");
                        break;
                    }

                    SPItemVM? bestItem = null;
                    float bestProfitDensity = -1f;
                    bool hasSwapped = false;

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
                                settings.CraftingMaterialsTradingMode,
                                true))
                        {
                            continue;
                        }

                        int boughtSoFar = boughtQuantities[item];
                        int initialMerchantCount = item.ItemCount;

                        if (boughtSoFar >= initialMerchantCount) continue;

                        float itemWeight = itemObj.Weight;
                        var playerItem = vm.RightItemListVM?.FirstOrDefault(r => r.ItemRosterElement.EquipmentElement.Item == itemObj);
                        
                        int currentPrice = logic != null ? logic.GetItemPrice(item.ItemRosterElement.EquipmentElement, true) : itemObj.Value;
                        float avgPrice = PricingService.GetReferencePrice(
                            logic,
                            item.ItemRosterElement.EquipmentElement,
                            null,
                            0f,
                            isSelling: false
                        );

                        float unitProfit = avgPrice - currentPrice;
                        float itemWeightDivisor = itemWeight > 0.01f ? itemWeight : 0.01f;
                        float profitDensity = unitProfit / itemWeightDivisor;

                        string key = GetTradeIdentityKey(item.ItemRosterElement.EquipmentElement);
                        int extraSoldForThis = extraSoldQuantities.TryGetValue(key, out int esVal) ? esVal : 0;
                        int currentlyOwned = (playerItem != null ? playerItem.ItemCount : 0) + boughtSoFar - extraSoldForThis;

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
                                float freeCargo = tradeContext.FreeCargoHeadroom - netWeightAdded;
                                if (freeCargo < itemWeight)
                                {
                                    // Try margin replacement: sell lower-margin owned items to merchant to make room for bestItem
                                    SPItemVM? swapItem = null;
                                    float worstOwnProfitDensity = float.MaxValue;

                                    if (vm.RightItemListVM != null)
                                    {
                                        foreach (var ownItem in vm.RightItemListVM)
                                        {
                                            if (ownItem == null || ownItem.ItemRosterElement.EquipmentElement.Item == null) continue;

                                            var ownItemObj = ownItem.ItemRosterElement.EquipmentElement.Item;
                                            if (ownItemObj == itemObj) continue;
                                            if (!TradeCandidatePolicy.CanTradeByMode(
                                                    ownItemObj,
                                                    settings.FoodTradingMode,
                                                    settings.LivestockTradingMode,
                                                    settings.MountsTradingMode,
                                                    settings.CraftingMaterialsTradingMode,
                                                    false))
                                            {
                                                continue;
                                            }

                                            var sellableEntry = tradeContext.SellableItems.FirstOrDefault(s => s.Matches(ownItem.ItemRosterElement.EquipmentElement));
                                            string ownKey = GetTradeIdentityKey(ownItem.ItemRosterElement.EquipmentElement);
                                            int extraSold = extraSoldQuantities.TryGetValue(ownKey, out int es) ? es : 0;
                                            int maxSellable = (sellableEntry?.AvailableQuantity ?? 0) - extraSold;
                                            if (maxSellable <= 0) continue;

                                            float ownAvgPrice = PricingService.GetReferencePrice(
                                                logic,
                                                ownItem.ItemRosterElement.EquipmentElement,
                                                ownItem.ItemRosterElement,
                                                ownItem.ItemCost,
                                                isSelling: true
                                            );
                                            
                                            int ownSellPrice = logic != null ? logic.GetItemPrice(ownItem.ItemRosterElement.EquipmentElement, false) : 0;
                                            if (ownSellPrice <= 0) continue;

                                            float ownWeight = ownItemObj.Weight;
                                            float ownWeightDivisor = ownWeight > 0.01f ? ownWeight : 0.01f;
                                            float ownFutureProfitDensity = (ownAvgPrice - ownSellPrice) / ownWeightDivisor;

                                            if (ownFutureProfitDensity < worstOwnProfitDensity)
                                            {
                                                worstOwnProfitDensity = ownFutureProfitDensity;
                                                swapItem = ownItem;
                                            }
                                        }
                                    }

                                    if (swapItem != null && worstOwnProfitDensity < profitDensity)
                                    {
                                        var swapItemObj = swapItem.ItemRosterElement.EquipmentElement.Item;
                                        int swapPrice = logic != null ? logic.GetItemPrice(swapItem.ItemRosterElement.EquipmentElement, false) : 0;

                                        WriteLog($"[Margin Swap] Selling 1x {swapItemObj.Name} (Future Profit Density: {worstOwnProfitDensity:F1}) to buy 1x {itemObj.Name} (Buy Profit Density: {profitDensity:F1})");

                                        if (!settings.SimulationMode && logic != null && Hero.MainHero != null)
                                        {
                                            var command = TransferCommand.Transfer(
                                                1,
                                                InventoryLogic.InventorySide.PlayerInventory,
                                                InventoryLogic.InventorySide.OtherInventory,
                                                new ItemRosterElement(swapItem.ItemRosterElement.EquipmentElement, 1),
                                                EquipmentIndex.None,
                                                EquipmentIndex.None,
                                                Hero.MainHero.CharacterObject
                                            );
                                            logic.AddTransferCommand(command);
                                        }
                                        else if (!settings.SimulationMode)
                                        {
                                            swapItem.ExecuteSellSingle();
                                        }

                                        currentBalance += swapPrice;
                                        netWeightAdded -= swapItemObj.Weight;

                                        string swapKey = GetTradeIdentityKey(swapItem.ItemRosterElement.EquipmentElement);
                                        extraSoldQuantities[swapKey] = (extraSoldQuantities.TryGetValue(swapKey, out int esq) ? esq : 0) + 1;

                                        // Record swap-sell in report
                                        var existingSold = report.SoldItems.FirstOrDefault(s => s.Name == swapItemObj.Name.ToString());
                                        if (existingSold != null)
                                        {
                                            existingSold.Count++;
                                            existingSold.Gold += swapPrice;
                                        }
                                        else
                                        {
                                            report.SoldItems.Add(new TradedItemInfo
                                            {
                                                Name = swapItemObj.Name.ToString(),
                                                Count = 1,
                                                Gold = swapPrice,
                                                MarketPrice = PricingService.GetWorldAveragePrice(swapItem.ItemRosterElement.EquipmentElement)
                                            });
                                        }
                                        report.SoldNormalItems.Add(swapItemObj.Name.ToString());

                                        hasSwapped = true;
                                        break; 
                                    }
                                    else
                                    {
                                        skipReason = "Overburdened";
                                    }
                                }
                            }
                        }

                        if (skipReason == "" && settings.Stance == TradingStance.Balanced)
                        {
                            float currentWeightNow = startWeight + netWeightAdded;
                            float fullness = usableCapacity > 0f ? (currentWeightNow / usableCapacity) : 0f;
                            float limit = settings.CargoLimitThreshold;
                            if (fullness >= limit)
                            {
                                float goodBuyLimit = avgPrice * settings.GoodBuyThreshold;
                                if (currentPrice > goodBuyLimit)
                                {
                                    skipReason = $"CargoNearLimit (fullness={fullness:P0}, price={currentPrice} > limit={goodBuyLimit:F1})";
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
                        if (skipReason == "" && localExcludedItems.Contains(GetTradeIdentityKey(item.ItemRosterElement.EquipmentElement)))
                        {
                            skipReason = "SoldInSameStop";
                        }

                        // avgPrice already resolved early in loop

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
                            if (currentPrice < avgPrice && buySkipDiagnosticsWritten < MaxBuySkipDiagnostics)
                            {
                                string diagnosticKey = $"{GetTradeIdentityKey(item.ItemRosterElement.EquipmentElement)}:{skipReason}";
                                if (loggedSkipDiagnostics.Add(diagnosticKey))
                                {
                                    WriteLog($"[Buy Skip Diagnostic] {itemObj.Name}: {skipReason}");
                                    buySkipDiagnosticsWritten++;
                                }
                            }
                            else if (currentPrice < avgPrice && !buySkipDiagnosticsSuppressed)
                            {
                                WriteLog($"[Buy Skip Diagnostic] Additional buy-skip diagnostics suppressed after {MaxBuySkipDiagnostics} unique entries.");
                                buySkipDiagnosticsSuppressed = true;
                            }
                            continue;
                        }

                        if (profitDensity > bestProfitDensity)
                        {
                            bestProfitDensity = profitDensity;
                            bestItem = item;
                        }
                    }

                    if (hasSwapped)
                    {
                        continue;
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

                    if (!settings.SimulationMode && logic != null && Hero.MainHero != null)
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
                    else if (!settings.SimulationMode)
                    {
                        bestItem.ExecuteBuySingle();
                    }

                    boughtQuantities[bestItem] = (boughtQuantities.TryGetValue(bestItem, out int bq) ? bq : 0) + 1;
                    totalGoldSpentMap[bestItem] = (totalGoldSpentMap.TryGetValue(bestItem, out int tg) ? tg : 0) + price;
                    currentBalance -= price;
                    netWeightAdded += bestItemObj.Weight;

                    float dbgAvg = PricingService.GetReferencePrice(
                        logic,
                        bestItem.ItemRosterElement.EquipmentElement,
                        null,
                        0f,
                        isSelling: false
                    );
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

        internal static string GetTradeIdentityKey(EquipmentElement equipmentElement)
        {
            string itemId = equipmentElement.Item?.StringId ?? string.Empty;
            string modifierId = equipmentElement.ItemModifier?.StringId ?? string.Empty;
            return $"{itemId}::{modifierId}";
        }

    }
}
