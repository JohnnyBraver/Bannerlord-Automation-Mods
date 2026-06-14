using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using SettlementAutomationCore;

namespace TradeOptimizer
{
    public class TradeOptimizerProvider : IPreSellProvider, IFreeTradeAnalyzer
    {
        public string ProviderName => "TradeOptimizer";

        private static Settlement? _currentTradeSettlement = null;
        private static int _initialGold = 0;
        private static TradeTransactionReport? _accumulatedReport = null;

        public TradeOptimizerProvider()
        {
            SettlementAutomationCore.SubModule.OnAutomationCycleCompleted += OnAutomationCycleCompleted;
        }

        private void OnAutomationCycleCompleted(Settlement settlement)
        {
            try
            {
                if (_currentTradeSettlement == settlement && _accumulatedReport != null)
                {
                    int finalGold = Hero.MainHero?.Gold ?? 0;
                    bool anythingTraded = _accumulatedReport.SoldItems.Count > 0 || _accumulatedReport.BoughtItems.Count > 0;
                    TradingPatches.PrintTradeReport(finalGold, _initialGold, _accumulatedReport, settlement.Name.ToString());
                    // Print cargo only after ALL phases have completed so the weight is accurate
                    if (anythingTraded)
                    {
                        bool isSim = Settings.Instance?.SimulationMode ?? false;
                        TradingPatches.PrintCargoStatus(isSim);
                    }
                }
            }
            catch (Exception ex)
            {
                TradingEngine.WriteLog($"Error printing final trade report: {ex}");
            }
            finally
            {
                _currentTradeSettlement = null;
                _accumulatedReport = null;
            }
        }

        private List<TradeOrder> SimulateAndCollectOrders(MobileParty party, Settlement settlement, bool runSell, bool runBuy, HashSet<string>? excludedItems = null, bool isMainCall = true)
        {
            var orders = new List<TradeOrder>();
            var settings = Settings.Instance;
            if (settings == null || !settings.AutoTradeOnEnterSettlement) return orders;

            // Check if enough campaign days have passed to let the initial economy stabilize
            float elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
            if (elapsedDays < settings.InitialSettlementDaysDelay)
            {
                TradingEngine.WriteLog($"[Settling Period] Skipped auto-trade simulation for {settlement.Name} (Campaign Day {elapsedDays:F1} < Settling Limit {settings.InitialSettlementDaysDelay})");
                return orders;
            }

            // Back up the rosters so we can simulate on the real rosters to get accurate dynamic prices
            // while restoring the original state completely after the simulation run finishes.
            var backupPlayerRoster = new TaleWorlds.CampaignSystem.Roster.ItemRoster(party.ItemRoster);
            var backupSettlementRoster = new TaleWorlds.CampaignSystem.Roster.ItemRoster(settlement.ItemRoster);

            try
            {
                var tempLogic = SettlementAutomationCore.Helpers.InventoryHelper.CreateAndInitInventoryLogic(party, settlement, false);
                if (tempLogic == null) return orders;

                Func<TaleWorlds.Core.WeaponComponentData, TaleWorlds.Core.ItemObject.ItemUsageSetFlags> dummyFunc = w => (TaleWorlds.Core.ItemObject.ItemUsageSetFlags)0;
                var vm = new SPInventoryVM(tempLogic, false, dummyFunc);

                // Capture initial counts on both sides to determine net transfers later
                var initialPlayerCounts = new Dictionary<string, int>();
                var playerElements = tempLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
                for (int i = 0; i < playerElements.Count; i++)
                {
                    var el = playerElements[i];
                    if (el.EquipmentElement.Item != null)
                    {
                        string key = el.EquipmentElement.Item.StringId;
                        initialPlayerCounts[key] = initialPlayerCounts.TryGetValue(key, out int count) ? count + el.Amount : el.Amount;
                    }
                }

                // Map StringId to EquipmentElement to construct TradeOrder later
                var eqElementMap = new Dictionary<string, EquipmentElement>();
                for (int i = 0; i < playerElements.Count; i++)
                {
                    var el = playerElements[i];
                    if (el.EquipmentElement.Item != null)
                    {
                        eqElementMap[el.EquipmentElement.Item.StringId] = el.EquipmentElement;
                    }
                }
                var otherElements = tempLogic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory);
                for (int i = 0; i < otherElements.Count; i++)
                {
                    var el = otherElements[i];
                    if (el.EquipmentElement.Item != null)
                    {
                        eqElementMap[el.EquipmentElement.Item.StringId] = el.EquipmentElement;
                    }
                }

                // Run the actual optimization on the temp logic (mutates tempLogic, which mutates real rosters)
                var report = TradingEngine.RunOptimization(vm, runSell, runBuy, excludedItems);

                // Compute net difference to determine what actually got traded in the simulation
                var finalPlayerCounts = new Dictionary<string, int>();
                var finalPlayerElements = tempLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
                for (int i = 0; i < finalPlayerElements.Count; i++)
                {
                    var el = finalPlayerElements[i];
                    if (el.EquipmentElement.Item != null)
                    {
                        string key = el.EquipmentElement.Item.StringId;
                        finalPlayerCounts[key] = finalPlayerCounts.TryGetValue(key, out int count) ? count + el.Amount : el.Amount;
                    }
                }

                // Adjust final counts to subtract yields of slaughter arbitrage
                foreach (var slaughter in report.ArbitrageSlaughters)
                {
                    var item = slaughter.EqElement.Item;
                    if (item != null && item.HorseComponent != null)
                    {
                        int meatYield = item.HorseComponent.MeatCount * slaughter.Amount;
                        int hidesYield = item.HorseComponent.HideCount * slaughter.Amount;

                        string meatId = DefaultItems.Meat.StringId;
                        string hidesId = DefaultItems.Hides.StringId;

                        if (meatYield > 0 && finalPlayerCounts.ContainsKey(meatId))
                        {
                            finalPlayerCounts[meatId] = Math.Max(0, finalPlayerCounts[meatId] - meatYield);
                        }
                        if (hidesYield > 0 && finalPlayerCounts.ContainsKey(hidesId))
                        {
                            finalPlayerCounts[hidesId] = Math.Max(0, finalPlayerCounts[hidesId] - hidesYield);
                        }
                    }
                }

                // All unique items across initial & final player inventory
                var allKeys = initialPlayerCounts.Keys.Union(finalPlayerCounts.Keys).Distinct();

                foreach (var key in allKeys)
                {
                    int initialCount = initialPlayerCounts.TryGetValue(key, out int c1) ? c1 : 0;
                    int finalCount = finalPlayerCounts.TryGetValue(key, out int c2) ? c2 : 0;
                    int diff = finalCount - initialCount; // positive means we bought, negative means we sold

                    if (diff > 0)
                    {
                        orders.Add(new TradeOrder(eqElementMap[key], diff, true));
                    }
                    else if (diff < 0)
                    {
                        orders.Add(new TradeOrder(eqElementMap[key], -diff, false));
                    }
                }

                // Manually append the buy and slaughter orders for arbitrage slaughters
                foreach (var slaughter in report.ArbitrageSlaughters)
                {
                    orders.Add(new TradeOrder(slaughter.EqElement, slaughter.Amount, true, false));
                    orders.Add(new TradeOrder(slaughter.EqElement, slaughter.Amount, false, true));
                }

                // Accumulate transaction report for final printing
                if (isMainCall && orders.Count > 0 && Hero.MainHero != null)
                {
                    if (_currentTradeSettlement != settlement)
                    {
                        _currentTradeSettlement = settlement;
                        _initialGold = Hero.MainHero.Gold;
                        _accumulatedReport = new TradeTransactionReport();
                    }

                    if (_accumulatedReport != null)
                    {
                        // Merge sold items
                        foreach (var s in report.SoldItems)
                        {
                            int existingIdx = _accumulatedReport.SoldItems.FindIndex(x => x.Name == s.Name);
                            if (existingIdx >= 0)
                            {
                                var old = _accumulatedReport.SoldItems[existingIdx];
                                old.Count += s.Count;
                                old.Gold += s.Gold;
                                if (s.MarketPrice > 0) old.MarketPrice = s.MarketPrice;
                            }
                            else
                            {
                                _accumulatedReport.SoldItems.Add(new TradedItemInfo
                                {
                                    Name = s.Name,
                                    Count = s.Count,
                                    Gold = s.Gold,
                                    MarketPrice = s.MarketPrice
                                });
                            }
                        }

                        // Merge bought items
                        foreach (var b in report.BoughtItems)
                        {
                            int existingIdx = _accumulatedReport.BoughtItems.FindIndex(x => x.Name == b.Name);
                            if (existingIdx >= 0)
                            {
                                var old = _accumulatedReport.BoughtItems[existingIdx];
                                old.Count += b.Count;
                                old.Gold += b.Gold;
                                if (b.MarketPrice > 0) old.MarketPrice = b.MarketPrice;
                            }
                            else
                            {
                                _accumulatedReport.BoughtItems.Add(new TradedItemInfo
                                {
                                    Name = b.Name,
                                    Count = b.Count,
                                    Gold = b.Gold,
                                    MarketPrice = b.MarketPrice
                                });
                            }
                        }

                        // Merge arbitrage slaughters
                        foreach (var s in report.ArbitrageSlaughters)
                        {
                            _accumulatedReport.ArbitrageSlaughters.Add(s);
                        }
                    }
                }
            }
            finally
            {
                // Restore original rosters
                party.ItemRoster.Clear();
                party.ItemRoster.Add(backupPlayerRoster);

                settlement.ItemRoster.Clear();
                settlement.ItemRoster.Add(backupSettlementRoster);
            }

            return orders;
        }

        // IPreSellProvider: handles XP-farm split transaction selling (pre-sell phase)
        public List<TradeOrder> GetPreSellOrders(MobileParty party, Settlement settlement)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ShouldSplitTransactions)
            {
                return new List<TradeOrder>();
            }
            return SimulateAndCollectOrders(party, settlement, runSell: true, runBuy: false, isMainCall: true);
        }

        // IFreeTradeAnalyzer: analyzes market and returns a TradeProposal within the given context limits
        public TradeProposal AnalyzeMarket(TradeContext context)
        {
            var actions = new List<TradeAction>();
            var settings = Settings.Instance;
            if (settings == null || !settings.AutoTradeOnEnterSettlement)
                return new TradeProposal(actions);

            var party = context.Party;
            var settlement = context.Settlement;

            // Check settling period
            float elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
            if (elapsedDays < settings.InitialSettlementDaysDelay)
            {
                TradingEngine.WriteLog($"[Settling Period] Skipped free trade for {settlement.Name} (Day {elapsedDays:F1} < Limit {settings.InitialSettlementDaysDelay})");
                return new TradeProposal(actions);
            }

            // Build a synthetic sellable map for TradingEngine to use (from context.SellableItems)
            // The TradingEngine uses the InventoryLogic in context for price queries
            // We simulate with rostert backups, but pass context limits to RunOptimization
            var backupPlayerRoster = new TaleWorlds.CampaignSystem.Roster.ItemRoster(party.ItemRoster);
            var backupSettlementRoster = new TaleWorlds.CampaignSystem.Roster.ItemRoster(settlement.ItemRoster);

            try
            {
                var tempLogic = SettlementAutomationCore.Helpers.InventoryHelper.CreateAndInitInventoryLogic(party, settlement, false);
                if (tempLogic == null) return new TradeProposal(actions);

                Func<TaleWorlds.Core.WeaponComponentData, TaleWorlds.Core.ItemObject.ItemUsageSetFlags> dummyFunc = w => (TaleWorlds.Core.ItemObject.ItemUsageSetFlags)0;
                var vm = new SPInventoryVM(tempLogic, false, dummyFunc);

                // Capture initial counts
                var initialPlayerCounts = new Dictionary<string, int>();
                var eqElementMap = new Dictionary<string, EquipmentElement>();
                var playerElements = tempLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
                for (int i = 0; i < playerElements.Count; i++)
                {
                    var el = playerElements[i];
                    if (el.EquipmentElement.Item != null)
                    {
                        string key = el.EquipmentElement.Item.StringId;
                        initialPlayerCounts[key] = initialPlayerCounts.TryGetValue(key, out int count) ? count + el.Amount : el.Amount;
                        eqElementMap[key] = el.EquipmentElement;
                    }
                }
                var otherElements = tempLogic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory);
                for (int i = 0; i < otherElements.Count; i++)
                {
                    var el = otherElements[i];
                    if (el.EquipmentElement.Item != null)
                        eqElementMap[el.EquipmentElement.Item.StringId] = el.EquipmentElement;
                }

                // If split transactions, for free trade we only run buy phase (sell was handled in pre-sell)
                bool runSell = !settings.ShouldSplitTransactions;
                bool runBuy = true;

                HashSet<string>? excludedSellItems = null;
                if (settings.ShouldSplitTransactions)
                {
                    // Pre-sell already handled the sell phase; exclude those items
                    var preSellOrders = SimulateAndCollectOrders(party, settlement, runSell: true, runBuy: false, isMainCall: false);
                    excludedSellItems = new HashSet<string>(preSellOrders.Where(o => !o.IsBuy).Select(o => o.EquipmentElement.Item?.Name.ToString() ?? ""));
                }

                var report = TradingEngine.RunOptimization(vm, runSell, runBuy, excludedSellItems, context);

                // Compute net changes
                var finalPlayerCounts = new Dictionary<string, int>();
                var finalPlayerElements = tempLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
                for (int i = 0; i < finalPlayerElements.Count; i++)
                {
                    var el = finalPlayerElements[i];
                    if (el.EquipmentElement.Item != null)
                    {
                        string key = el.EquipmentElement.Item.StringId;
                        finalPlayerCounts[key] = finalPlayerCounts.TryGetValue(key, out int count) ? count + el.Amount : el.Amount;
                        if (!eqElementMap.ContainsKey(key))
                            eqElementMap[key] = el.EquipmentElement;
                    }
                }

                // Adjust for slaughter yields
                foreach (var slaughter in report.ArbitrageSlaughters)
                {
                    var item = slaughter.EqElement.Item;
                    if (item?.HorseComponent != null)
                    {
                        int meatYield = item.HorseComponent.MeatCount * slaughter.Amount;
                        int hidesYield = item.HorseComponent.HideCount * slaughter.Amount;
                        string meatId = DefaultItems.Meat.StringId;
                        string hidesId = DefaultItems.Hides.StringId;
                        if (meatYield > 0 && finalPlayerCounts.ContainsKey(meatId))
                            finalPlayerCounts[meatId] = Math.Max(0, finalPlayerCounts[meatId] - meatYield);
                        if (hidesYield > 0 && finalPlayerCounts.ContainsKey(hidesId))
                            finalPlayerCounts[hidesId] = Math.Max(0, finalPlayerCounts[hidesId] - hidesYield);
                    }
                }

                var allKeys = initialPlayerCounts.Keys.Union(finalPlayerCounts.Keys).Distinct();
                foreach (var key in allKeys)
                {
                    if (!eqElementMap.ContainsKey(key)) continue;
                    int initialCount = initialPlayerCounts.TryGetValue(key, out int c1) ? c1 : 0;
                    int finalCount = finalPlayerCounts.TryGetValue(key, out int c2) ? c2 : 0;
                    int diff = finalCount - initialCount;

                    if (diff > 0)
                    {
                        actions.Add(new TradeAction(eqElementMap[key], diff, TradeActionType.Buy));
                    }
                    else if (diff < 0)
                    {
                        // Only propose sell if item is in context's sellable list
                        var sellable = context.SellableItems.FirstOrDefault(s => s.EquipmentElement.Item?.StringId == key);
                        int sellQty = Math.Min(-diff, sellable?.AvailableQuantity ?? 0);
                        if (sellQty > 0)
                            actions.Add(new TradeAction(eqElementMap[key], sellQty, TradeActionType.Sell));
                    }
                }

                // Append slaughter actions
                foreach (var slaughter in report.ArbitrageSlaughters)
                {
                    actions.Add(new TradeAction(slaughter.EqElement, slaughter.Amount, TradeActionType.Buy));
                    actions.Add(new TradeAction(slaughter.EqElement, slaughter.Amount, TradeActionType.Slaughter));
                }

                // Accumulate report for final printing
                if (report.SoldItems.Count > 0 || report.BoughtItems.Count > 0)
                {
                    if (_currentTradeSettlement != settlement && Hero.MainHero != null)
                    {
                        _currentTradeSettlement = settlement;
                        _initialGold = Hero.MainHero.Gold;
                        _accumulatedReport = new TradeTransactionReport();
                    }

                    if (_accumulatedReport != null)
                    {
                        foreach (var s in report.SoldItems)
                        {
                            int existingIdx = _accumulatedReport.SoldItems.FindIndex(x => x.Name == s.Name);
                            if (existingIdx >= 0) { _accumulatedReport.SoldItems[existingIdx].Count += s.Count; _accumulatedReport.SoldItems[existingIdx].Gold += s.Gold; }
                            else _accumulatedReport.SoldItems.Add(new TradedItemInfo { Name = s.Name, Count = s.Count, Gold = s.Gold, MarketPrice = s.MarketPrice });
                        }
                        foreach (var b in report.BoughtItems)
                        {
                            int existingIdx = _accumulatedReport.BoughtItems.FindIndex(x => x.Name == b.Name);
                            if (existingIdx >= 0) { _accumulatedReport.BoughtItems[existingIdx].Count += b.Count; _accumulatedReport.BoughtItems[existingIdx].Gold += b.Gold; }
                            else _accumulatedReport.BoughtItems.Add(new TradedItemInfo { Name = b.Name, Count = b.Count, Gold = b.Gold, MarketPrice = b.MarketPrice });
                        }
                        foreach (var sl in report.ArbitrageSlaughters)
                            _accumulatedReport.ArbitrageSlaughters.Add(sl);
                    }
                }
            }
            finally
            {
                party.ItemRoster.Clear();
                party.ItemRoster.Add(backupPlayerRoster);
                settlement.ItemRoster.Clear();
                settlement.ItemRoster.Add(backupSettlementRoster);
            }

            return new TradeProposal(actions);
        }
    }
}
