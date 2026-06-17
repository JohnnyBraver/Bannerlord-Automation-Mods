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
    public class TradeOptimizerProvider : IPreSellProvider, IFreeTradeAnalyzer, IAutomationReportProvider
    {
        public string ProviderName => "TradeOptimizer";

        public IReadOnlyList<string> BuildAutomationReportLines(AutomationReportContext context)
        {
            var lines = new List<string>();
            if (context == null || context.Stage != AutomationTransactionStage.FreeTrade)
            {
                return lines;
            }

            var settings = Settings.Instance;
            var reportMode = settings?.TradeReportDetail ?? TradeReportDetailMode.TopTradeGoods;
            return BuildAutomationReportLines(
                context.Stage,
                context.BoughtItems,
                context.SoldItems,
                context.SlaughteredItems,
                reportMode,
                Math.Max(1, settings?.TopTradeGoodsToReport ?? 5),
                settings?.TradeReportSort ?? TradeReportSortMode.PaidPrice,
                settings?.ApplyTradeReportLimitPerSide ?? true,
                GetSettlementName(context));
        }

        internal static IReadOnlyList<string> BuildAutomationReportLines(
            AutomationTransactionStage stage,
            IReadOnlyList<AutomationReportItem> boughtItems,
            IReadOnlyList<AutomationReportItem> soldItems,
            IReadOnlyList<AutomationReportItem> slaughteredItems,
            TradeReportDetailMode reportMode,
            int topTradeGoodsLimit,
            TradeReportSortMode sortMode,
            bool limitPerSide = true,
            string settlementName = "Settlement")
        {
            var lines = new List<string>();
            if (stage != AutomationTransactionStage.FreeTrade)
            {
                return lines;
            }

            if (reportMode == TradeReportDetailMode.Full)
            {
                string summary = BuildFullReportSummary(boughtItems, soldItems, slaughteredItems);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    lines.Add($"[Trade] Free trade @ {settlementName}: {summary}");
                }

                return lines;
            }

            string topTradeGoodsSummary = BuildTopTradeGoodsSummary(boughtItems, soldItems, topTradeGoodsLimit, sortMode, limitPerSide);
            if (string.IsNullOrWhiteSpace(topTradeGoodsSummary))
            {
                return lines;
            }

            lines.Add($"[Trade] Free trade @ {settlementName}: {topTradeGoodsSummary}");
            return lines;
        }

        private static string BuildTopTradeGoodsSummary(
            IReadOnlyList<AutomationReportItem> boughtItems,
            IReadOnlyList<AutomationReportItem> soldItems,
            int topTradeGoodsLimit,
            TradeReportSortMode sortMode,
            bool limitPerSide)
        {
            int limit = Math.Max(1, topTradeGoodsLimit);
            var parts = new List<string>();
            var sold = soldItems
                .Where(item => item.Category == InventoryItemCategory.TradeGood)
                .Select(item => new ReportItemWithDirection(item, isSale: true))
                .ToList();
            var bought = boughtItems
                .Where(item => item.Category == InventoryItemCategory.TradeGood)
                .Select(item => new ReportItemWithDirection(item, isSale: false))
                .ToList();

            IReadOnlyList<ReportItemWithDirection> topSold;
            IReadOnlyList<ReportItemWithDirection> topBought;
            if (limitPerSide)
            {
                topSold = SortReportItems(sold, sortMode).Take(limit).ToList();
                topBought = SortReportItems(bought, sortMode).Take(limit).ToList();
            }
            else
            {
                var combined = SortReportItems(sold.Concat(bought), sortMode)
                    .Take(limit)
                    .ToList();
                topSold = combined.Where(item => item.IsSale).ToList();
                topBought = combined.Where(item => !item.IsSale).ToList();
            }

            if (topSold.Count > 0)
            {
                parts.Add($"sold {FormatReportItems(topSold, includeGold: true)}");
            }
            if (topBought.Count > 0)
            {
                parts.Add($"bought {FormatReportItems(topBought, includeGold: true)}");
            }

            return string.Join("; ", parts);
        }

        private static string GetSettlementName(AutomationReportContext context)
        {
            return context.Settlement?.Name?.ToString() ?? "Settlement";
        }

        private static string BuildFullReportSummary(
            IReadOnlyList<AutomationReportItem> boughtItems,
            IReadOnlyList<AutomationReportItem> soldItems,
            IReadOnlyList<AutomationReportItem> slaughteredItems)
        {
            var parts = new List<string>();
            if (soldItems.Count > 0)
            {
                parts.Add($"sold {FormatReportItems(soldItems, includeGold: true, isSale: true)}");
            }
            if (boughtItems.Count > 0)
            {
                parts.Add($"bought {FormatReportItems(boughtItems, includeGold: true, isSale: false)}");
            }
            if (slaughteredItems.Count > 0)
            {
                parts.Add($"slaughtered {FormatReportItems(slaughteredItems, includeGold: false)}");
            }

            return string.Join("; ", parts);
        }

        private static IEnumerable<ReportItemWithDirection> SortReportItems(IEnumerable<ReportItemWithDirection> items, TradeReportSortMode sortMode)
        {
            switch (sortMode)
            {
                case TradeReportSortMode.Amount:
                    return items.OrderByDescending(item => item.Item.Quantity).ThenBy(item => item.Item.ItemName);
                case TradeReportSortMode.MarketValue:
                    return items.OrderByDescending(item => item.Item.MarketValue).ThenByDescending(item => item.Item.Gold).ThenBy(item => item.Item.ItemName);
                case TradeReportSortMode.PaidPrice:
                default:
                    return items.OrderByDescending(item => item.Item.Gold).ThenByDescending(item => item.Item.MarketValue).ThenBy(item => item.Item.ItemName);
            }
        }

        private static string FormatReportItems(IReadOnlyList<AutomationReportItem> items, bool includeGold, bool isSale = false)
        {
            return string.Join(", ", items.Select(item => FormatReportItem(item, includeGold, isSale)));
        }

        private static string FormatReportItems(IReadOnlyList<ReportItemWithDirection> items, bool includeGold)
        {
            return string.Join(", ", items.Select(item => FormatReportItem(item.Item, includeGold, item.IsSale)));
        }

        private static string FormatReportItem(AutomationReportItem item, bool includeGold, bool isSale)
        {
            if (!includeGold || item.Gold == 0)
            {
                return $"{item.Quantity}x {item.ItemName}";
            }

            string sign = isSale ? "+" : "-";
            return $"{item.Quantity}x {item.ItemName} ({sign}{Math.Abs(item.Gold)}d)";
        }

        private sealed class ReportItemWithDirection
        {
            public ReportItemWithDirection(AutomationReportItem item, bool isSale)
            {
                Item = item;
                IsSale = isSale;
            }

            public AutomationReportItem Item { get; }
            public bool IsSale { get; }
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
                var sellableItems = playerElements
                    .Where(el => el.EquipmentElement.Item != null)
                    .Select(el => new SellableItem(el.EquipmentElement, el.Amount))
                    .ToList();
                var tradeContext = TradeContextFactory.Create(party, settlement, tempLogic, sellableItems);
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
                var report = TradingEngine.RunOptimization(vm, runSell, runBuy, tradeContext, excludedItems);

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

                var report = TradingEngine.RunOptimization(vm, runSell, runBuy, context);

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
