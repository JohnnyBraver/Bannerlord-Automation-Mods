using TaleWorlds.Core;
using TradeOptimizer;
using Xunit;

namespace TradeOptimizer.Tests
{
    public class TradeCandidatePolicyTests
    {
        [Fact]
        public void CanTradeByMode_RejectsNonCommodities()
        {
            var item = new ItemObject("sword");

            Assert.False(TradeCandidatePolicy.IsCommodityCandidate(item));
            Assert.False(TradeCandidatePolicy.CanTradeByMode(
                item,
                TradingMode.BuyAndSell,
                TradingMode.BuyAndSell,
                TradingMode.BuyAndSell,
                isBuy: true));
        }

        [Theory]
        [InlineData(TradingMode.None, true, false)]
        [InlineData(TradingMode.None, false, false)]
        [InlineData(TradingMode.BuyOnly, true, true)]
        [InlineData(TradingMode.BuyOnly, false, false)]
        [InlineData(TradingMode.SellOnly, true, false)]
        [InlineData(TradingMode.SellOnly, false, true)]
        [InlineData(TradingMode.BuyAndSell, true, true)]
        [InlineData(TradingMode.BuyAndSell, false, true)]
        public void CanTradeByMode_RespectsPerCategoryTradingMode(TradingMode foodMode, bool isBuy, bool expected)
        {
            bool allowed = TradeCandidatePolicy.CanTradeByMode(
                isFood: true,
                isLivestock: false,
                isMount: false,
                foodMode,
                TradingMode.BuyAndSell,
                TradingMode.BuyAndSell,
                isBuy);

            Assert.Equal(expected, allowed);
        }

        [Theory]
        [InlineData(80, 100, 0.80f, true)]
        [InlineData(81, 100, 0.80f, false)]
        [InlineData(80, 0, 0.80f, false)]
        public void PassesBuyPriceThreshold_UsesAveragePriceAndThreshold(int price, float average, float threshold, bool expected)
        {
            Assert.Equal(expected, TradeCandidatePolicy.PassesBuyPriceThreshold(price, average, threshold));
        }

        [Fact]
        public void CanSpendWithoutBreachingReserve_UsesGreaterOfGoldReserveAndWageReserve()
        {
            Assert.Equal(2000, TradeCandidatePolicy.GetMinimumRequiredBalance(1000, 200, 10));
            Assert.True(TradeCandidatePolicy.CanSpendWithoutBreachingReserve(3000, 1000, 1000, 200, 10));
            Assert.False(TradeCandidatePolicy.CanSpendWithoutBreachingReserve(2999, 1000, 1000, 200, 10));
        }
    }
}

