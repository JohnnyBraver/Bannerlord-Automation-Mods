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
using SettlementAutomationCore.Transactions;

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
            if (settings != null && !settings.ModEnabled)
            {
                return lines;
            }

            var reportMode = settings?.TradeReportDetail ?? TradeReportDetailMode.TopTradeGoods;
            var reportLines = BuildAutomationReportLines(
                context.Stage,
                context.BoughtItems,
                context.SoldItems,
                context.SlaughteredItems,
                reportMode,
                Math.Max(1, settings?.TopTradeGoodsToReport ?? 5),
                settings?.TradeReportSort ?? TradeReportSortMode.PaidPrice,
                settings?.ApplyTradeReportLimitPerSide ?? true,
                GetSettlementName(context));
            foreach (var line in reportLines)
            {
                TradingEngine.WriteLog($"[Execution Report] {line}");
            }

            return reportLines;
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

            string topItemsSummary = BuildTopItemsSummary(boughtItems, soldItems, topTradeGoodsLimit, sortMode, limitPerSide);
            if (string.IsNullOrWhiteSpace(topItemsSummary))
            {
                return lines;
            }

            lines.Add($"[Trade] Free trade @ {settlementName}: {topItemsSummary}");
            return lines;
        }

        private static string BuildTopItemsSummary(
            IReadOnlyList<AutomationReportItem> boughtItems,
            IReadOnlyList<AutomationReportItem> soldItems,
            int topTradeGoodsLimit,
            TradeReportSortMode sortMode,
            bool limitPerSide)
        {
            int limit = Math.Max(1, topTradeGoodsLimit);
            var parts = new List<string>();
            var sold = soldItems
                .Where(item => item.Quantity > 0)
                .Select(item => new ReportItemWithDirection(item, isSale: true))
                .ToList();
            var bought = boughtItems
                .Where(item => item.Quantity > 0)
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
            if (settings == null || !settings.ModEnabled || !settings.AutoTradeOnEnterSettlement) return orders;

            // Check if enough campaign days have passed to let the initial economy stabilize
            float elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
            if (elapsedDays < settings.InitialSettlementDaysDelay)
            {
                TradingEngine.WriteLog($"[Settling Period] Skipped auto-trade simulation for {settlement.Name} (Campaign Day {elapsedDays:F1} < Settling Limit {settings.InitialSettlementDaysDelay})");
                return orders;
            }

            using var pricingSession = TradePricingSession.CreateSimulated(party, settlement);
            if (pricingSession == null) return orders;
            var tempLogic = pricingSession.Logic;

            Func<TaleWorlds.Core.WeaponComponentData, TaleWorlds.Core.ItemObject.ItemUsageSetFlags> dummyFunc = w => (TaleWorlds.Core.ItemObject.ItemUsageSetFlags)0;
            var vm = new SPInventoryVM(tempLogic, false, dummyFunc);

            var playerElements = tempLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory);
            var lockKeys = InventoryLockHelper.GetCurrentLockKeys();
            var sellableItems = playerElements
                .Where(el => el.EquipmentElement.Item != null)
                .GroupBy(el => TradingEngine.GetTradeIdentityKey(el.EquipmentElement))
                .Select(g => new SellableItem(
                    g.First().EquipmentElement,
                    g.Sum(el => InventoryLockHelper.IsLocked(el.EquipmentElement, lockKeys) ? 0 : el.Amount)))
                .ToList();
            var tradeContext = TradeContextFactory.Create(party, settlement, tempLogic, sellableItems);

            // Run optimization on cloned trade rosters so demand-sensitive prices shift safely.
            var plan = TradingEngine.PlanOptimization(vm, runSell, runBuy, tradeContext, excludedItems, applyTransfers: true);
            if (settings.SimulationMode)
            {
                return orders;
            }

            orders.AddRange(plan.ToTradeOrders());
            return orders;
        }

        // IPreSellProvider: handles XP-farm split transaction selling (pre-sell phase)
        public List<TradeOrder> GetPreSellOrders(MobileParty party, Settlement settlement)
        {
            var settings = Settings.Instance;
            if (settings == null || !settings.ModEnabled || !settings.ShouldSplitTransactions)
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
            if (settings == null || !settings.ModEnabled || !settings.AutoTradeOnEnterSettlement)
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

            // Run demand-sensitive planning on cloned trade rosters so price shifts are preserved
            // without mutating the live party or settlement inventory.
            using var pricingSession = TradePricingSession.CreateSimulated(party, settlement);
            if (pricingSession == null) return new TradeProposal(actions);
            var tempLogic = pricingSession.Logic;

            Func<TaleWorlds.Core.WeaponComponentData, TaleWorlds.Core.ItemObject.ItemUsageSetFlags> dummyFunc = w => (TaleWorlds.Core.ItemObject.ItemUsageSetFlags)0;
            var vm = new SPInventoryVM(tempLogic, false, dummyFunc);

            bool runSell = !settings.ShouldSplitTransactions;
            bool runBuy = true;
            var planningContext = new TradeContext(
                context.Settlement,
                context.Party,
                tempLogic,
                context.AvailableGold,
                context.AvailableMerchantGold,
                context.SellPricesAreStatic,
                context.CargoCapacityBalance,
                context.EnforceCargoLimit,
                context.FreeAnimalSlots,
                context.MaxPackAnimalPurchases,
                context.SellableItems);

            var plan = TradingEngine.PlanOptimization(vm, runSell, runBuy, planningContext, applyTransfers: true);
            if (settings.SimulationMode)
            {
                return new TradeProposal(actions);
            }

            return plan.ToTradeProposal();
        }
    }
}
