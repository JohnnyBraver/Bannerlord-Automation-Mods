using TaleWorlds.Core;

namespace TradeOptimizer
{
    internal static class TradeCandidatePolicy
    {
        public static bool IsCommodityCandidate(ItemObject item)
        {
            return item != null && item.IsTradeGood;
        }

        public static bool CanTradeByMode(
            ItemObject item,
            TradingMode foodMode,
            TradingMode livestockMode,
            TradingMode mountsMode,
            bool isBuy)
        {
            if (!IsCommodityCandidate(item))
            {
                return false;
            }

            return CanTradeByMode(
                item.IsFood,
                item.IsAnimal && !item.IsMountable,
                item.IsMountable,
                foodMode,
                livestockMode,
                mountsMode,
                isBuy);
        }

        public static bool CanTradeByMode(
            bool isFood,
            bool isLivestock,
            bool isMount,
            TradingMode foodMode,
            TradingMode livestockMode,
            TradingMode mountsMode,
            bool isBuy)
        {
            var mode = TradingMode.BuyAndSell;
            if (isFood)
            {
                mode = foodMode;
            }
            else if (isLivestock)
            {
                mode = livestockMode;
            }
            else if (isMount)
            {
                mode = mountsMode;
            }

            if (mode == TradingMode.None)
            {
                return false;
            }

            if (isBuy)
            {
                return mode != TradingMode.SellOnly;
            }

            return mode != TradingMode.BuyOnly;
        }

        public static bool PassesBuyPriceThreshold(int currentPrice, float averagePrice, float buyPriceThresholdFactor)
        {
            return averagePrice > 0f && currentPrice <= averagePrice * buyPriceThresholdFactor;
        }

    }
}
