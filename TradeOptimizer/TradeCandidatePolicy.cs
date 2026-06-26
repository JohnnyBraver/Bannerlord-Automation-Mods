using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;

namespace TradeOptimizer
{
    internal static class TradeCandidatePolicy
    {
        private static readonly object CraftingMaterialLock = new object();
        private static HashSet<ItemObject>? CraftingMaterialItems;

        public static bool IsCommodityCandidate(ItemObject item)
        {
            return item != null && (item.IsTradeGood || item.IsAnimal || item.IsMountable);
        }

        public static bool IsCraftingMaterial(ItemObject item)
        {
            if (item == null)
            {
                return false;
            }

            var materialItems = GetCraftingMaterialItems();
            return materialItems.Contains(item);
        }

        public static bool CanTradeByMode(
            ItemObject item,
            TradingMode foodMode,
            TradingMode livestockMode,
            TradingMode mountsMode,
            TradingMode craftingMaterialMode,
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
                IsCraftingMaterial(item),
                foodMode,
                livestockMode,
                mountsMode,
                craftingMaterialMode,
                isBuy);
        }

        public static bool CanTradeByMode(
            bool isFood,
            bool isLivestock,
            bool isMount,
            bool isCraftingMaterial,
            TradingMode foodMode,
            TradingMode livestockMode,
            TradingMode mountsMode,
            TradingMode craftingMaterialMode,
            bool isBuy)
        {
            var mode = TradingMode.BuyAndSell;
            if (isCraftingMaterial)
            {
                mode = craftingMaterialMode;
            }
            else if (isFood)
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

        private static HashSet<ItemObject> GetCraftingMaterialItems()
        {
            lock (CraftingMaterialLock)
            {
                if (CraftingMaterialItems != null)
                {
                    return CraftingMaterialItems;
                }

                CraftingMaterialItems = new HashSet<ItemObject>();
                var smithingModel = Campaign.Current?.Models?.SmithingModel;
                if (smithingModel == null)
                {
                    return CraftingMaterialItems;
                }

                foreach (CraftingMaterials material in Enum.GetValues(typeof(CraftingMaterials)))
                {
                    var item = smithingModel.GetCraftingMaterialItem(material);
                    if (item != null)
                    {
                        CraftingMaterialItems.Add(item);
                    }
                }

                return CraftingMaterialItems;
            }
        }
    }
}
