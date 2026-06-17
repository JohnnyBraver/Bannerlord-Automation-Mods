using System.Collections.Generic;
using SettlementAutomationCore;
using TradeOptimizer;
using Xunit;

namespace TradeOptimizer.Tests
{
    public class TradeOptimizerReportTests
    {
        [Fact]
        public void Settings_DefaultsToConcisePaidPriceReport()
        {
            var settings = new Settings();

            Assert.Equal(TradeReportDetailMode.TopTradeGoods, settings.TradeReportDetail);
            Assert.Equal(4, settings.TopTradeGoodsToReport);
            Assert.True(settings.ApplyTradeReportLimitPerSide);
            Assert.Equal(TradeReportSortMode.PaidPrice, settings.TradeReportSort);
        }

        [Fact]
        public void BuildAutomationReportLines_DefaultsToTopTradeGoodsByPaidPrice()
        {
            var bought = new[]
                {
                    Item("Clay", InventoryItemCategory.TradeGood, 44, 352, 440),
                    Item("Grain", InventoryItemCategory.Food, 8, 80, 96),
                    Item("Iron Ore", InventoryItemCategory.TradeGood, 14, 798, 980)
                };
            var sold = new[]
                {
                    Item("Wine", InventoryItemCategory.TradeGood, 7, 490, 560),
                    Item("Tools", InventoryItemCategory.TradeGood, 3, 240, 300)
                };

            var lines = TradeOptimizerProvider.BuildAutomationReportLines(
                AutomationTransactionStage.FreeTrade,
                bought,
                sold,
                new List<AutomationReportItem>(),
                TradeReportDetailMode.TopTradeGoods,
                topTradeGoodsLimit: 2,
                TradeReportSortMode.PaidPrice,
                limitPerSide: true,
                settlementName: "Marunath");

            Assert.Equal("[Trade] Free trade @ Marunath: sold 7x Wine (+490d), 3x Tools (+240d); bought 14x Iron Ore (-798d), 44x Clay (-352d)", Assert.Single(lines));
        }

        [Fact]
        public void BuildAutomationReportLines_CanSortTopTradeGoodsByAmountOrMarketValue()
        {
            var bought = new[]
                {
                    Item("Clay", InventoryItemCategory.TradeGood, 44, 352, 440),
                    Item("Iron Ore", InventoryItemCategory.TradeGood, 14, 798, 980)
                };
            var sold = new[]
                {
                    Item("Wine", InventoryItemCategory.TradeGood, 7, 490, 560),
                    Item("Tools", InventoryItemCategory.TradeGood, 12, 360, 720)
                };

            var byAmount = TradeOptimizerProvider.BuildAutomationReportLines(
                AutomationTransactionStage.FreeTrade,
                bought,
                sold,
                new List<AutomationReportItem>(),
                TradeReportDetailMode.TopTradeGoods,
                topTradeGoodsLimit: 1,
                TradeReportSortMode.Amount,
                limitPerSide: true,
                settlementName: "Marunath");
            var byMarketValue = TradeOptimizerProvider.BuildAutomationReportLines(
                AutomationTransactionStage.FreeTrade,
                bought,
                sold,
                new List<AutomationReportItem>(),
                TradeReportDetailMode.TopTradeGoods,
                topTradeGoodsLimit: 1,
                TradeReportSortMode.MarketValue,
                limitPerSide: true,
                settlementName: "Marunath");

            Assert.Equal("[Trade] Free trade @ Marunath: sold 12x Tools (+360d); bought 44x Clay (-352d)", Assert.Single(byAmount));
            Assert.Equal("[Trade] Free trade @ Marunath: sold 12x Tools (+360d); bought 14x Iron Ore (-798d)", Assert.Single(byMarketValue));
        }

        [Fact]
        public void BuildAutomationReportLines_FullModeIncludesAllActivity()
        {
            var bought = new[]
                {
                    Item("Grain", InventoryItemCategory.Food, 8, 80, 96)
                };
            var sold = new[]
                {
                    Item("Wine", InventoryItemCategory.TradeGood, 7, 490, 560)
                };
            var slaughtered = new[]
                {
                    Item("Hog", InventoryItemCategory.Livestock, 2, 0, 120)
                };

            var lines = TradeOptimizerProvider.BuildAutomationReportLines(
                AutomationTransactionStage.FreeTrade,
                bought,
                sold,
                slaughtered,
                TradeReportDetailMode.Full,
                topTradeGoodsLimit: 1,
                TradeReportSortMode.PaidPrice,
                limitPerSide: true,
                settlementName: "Marunath");

            Assert.Equal("[Trade] Free trade @ Marunath: sold 7x Wine (+490d); bought 8x Grain (-80d); slaughtered 2x Hog", Assert.Single(lines));
        }

        [Fact]
        public void BuildAutomationReportLines_CanApplyLimitAcrossWholeReport()
        {
            var bought = new[]
            {
                Item("Clay", InventoryItemCategory.TradeGood, 44, 352, 440),
                Item("Iron Ore", InventoryItemCategory.TradeGood, 14, 798, 980)
            };
            var sold = new[]
            {
                Item("Wine", InventoryItemCategory.TradeGood, 7, 490, 560),
                Item("Tools", InventoryItemCategory.TradeGood, 12, 360, 720)
            };

            var lines = TradeOptimizerProvider.BuildAutomationReportLines(
                AutomationTransactionStage.FreeTrade,
                bought,
                sold,
                new List<AutomationReportItem>(),
                TradeReportDetailMode.TopTradeGoods,
                topTradeGoodsLimit: 2,
                TradeReportSortMode.PaidPrice,
                limitPerSide: false,
                settlementName: "Marunath");

            Assert.Equal("[Trade] Free trade @ Marunath: sold 7x Wine (+490d); bought 14x Iron Ore (-798d)", Assert.Single(lines));
        }

        private static AutomationReportItem Item(string name, InventoryItemCategory category, int quantity, int gold, int marketValue)
        {
            return new AutomationReportItem(name, category, quantity, gold, marketValue);
        }
    }
}
