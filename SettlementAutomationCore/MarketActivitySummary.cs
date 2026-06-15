using System.Collections.Generic;
using System.Linq;

namespace SettlementAutomationCore
{
    internal sealed class MarketActivitySummary
    {
        private const int InGameItemLimit = 4;

        private sealed class ItemActivity
        {
            public int Quantity { get; set; }
            public int Gold { get; set; }
        }

        private readonly Dictionary<string, ItemActivity> _boughtItems = new Dictionary<string, ItemActivity>();
        private readonly Dictionary<string, ItemActivity> _soldItems = new Dictionary<string, ItemActivity>();
        private readonly Dictionary<string, ItemActivity> _slaughteredItems = new Dictionary<string, ItemActivity>();

        public int GoldDelta { get; private set; }

        public bool HasActivity => _boughtItems.Count > 0 || _soldItems.Count > 0 || _slaughteredItems.Count > 0;
        public int TotalSoldGold => _soldItems.Values.Sum(item => item.Gold);
        public int TotalBoughtGold => _boughtItems.Values.Sum(item => item.Gold);

        public void AddBought(string itemName, int quantity, int gold)
        {
            AddItem(_boughtItems, itemName, quantity, gold);
        }

        public void AddSold(string itemName, int quantity, int gold)
        {
            AddItem(_soldItems, itemName, quantity, gold);
        }

        public void AddSlaughtered(string itemName, int quantity)
        {
            AddItem(_slaughteredItems, itemName, quantity, 0);
        }

        public void AddGoldDelta(int goldDelta)
        {
            GoldDelta += goldDelta;
        }

        public IReadOnlyList<string> BuildInGameLines(string settlementName, string? cargoStatus)
        {
            var lines = new List<string>();

            string salesPart = TotalSoldGold > 0 ? $"Sales: +{TotalSoldGold}d" : "Sales: none";
            string purchasesPart = TotalBoughtGold > 0 ? $"Purchases: -{TotalBoughtGold}d" : "Purchases: none";
            string netSign = GoldDelta >= 0 ? "+" : "";
            lines.Add($"[Automation] Market @ {settlementName} - {salesPart} | {purchasesPart} | Net {netSign}{GoldDelta}d");

            if (_soldItems.Count > 0)
            {
                lines.Add($"  Sold: {FormatItems(_soldItems, InGameItemLimit, includeGold: true, isSale: true)}");
            }
            if (_boughtItems.Count > 0)
            {
                lines.Add($"  Bought: {FormatItems(_boughtItems, InGameItemLimit, includeGold: true, isSale: false)}");
            }
            if (_slaughteredItems.Count > 0)
            {
                lines.Add($"  Slaughtered: {FormatItems(_slaughteredItems, InGameItemLimit, includeGold: false, isSale: false)}");
            }
            if (!string.IsNullOrWhiteSpace(cargoStatus))
            {
                lines.Add($"  Cargo: {cargoStatus}");
            }

            return lines;
        }

        public string BuildLogSummary(string settlementName)
        {
            var parts = new List<string>();
            if (_boughtItems.Count > 0)
            {
                parts.Add($"Bought: {FormatItems(_boughtItems, int.MaxValue, includeGold: true, isSale: false)}");
            }
            if (_soldItems.Count > 0)
            {
                parts.Add($"Sold: {FormatItems(_soldItems, int.MaxValue, includeGold: true, isSale: true)}");
            }
            if (_slaughteredItems.Count > 0)
            {
                parts.Add($"Slaughtered: {FormatItems(_slaughteredItems, int.MaxValue, includeGold: false, isSale: false)}");
            }

            string goldSign = GoldDelta >= 0 ? "+" : "";
            return $"Market automation summary at {settlementName} (Gold change: {goldSign}{GoldDelta}d). {string.Join(" ", parts)}";
        }

        private static void AddItem(Dictionary<string, ItemActivity> items, string itemName, int quantity, int gold)
        {
            if (quantity <= 0 || string.IsNullOrWhiteSpace(itemName)) return;

            if (items.TryGetValue(itemName, out var current))
            {
                current.Quantity += quantity;
                current.Gold += gold;
            }
            else
            {
                items.Add(itemName, new ItemActivity { Quantity = quantity, Gold = gold });
            }
        }

        private static string FormatItems(Dictionary<string, ItemActivity> items, int maxItems, bool includeGold, bool isSale)
        {
            var orderedItems = items
                .OrderByDescending(item => item.Value.Quantity)
                .ThenBy(item => item.Key)
                .ToList();

            var visibleItems = orderedItems
                .Take(maxItems)
                .Select(item => FormatItem(item.Key, item.Value, includeGold, isSale))
                .ToList();

            int hiddenCount = orderedItems.Count - visibleItems.Count;
            if (hiddenCount > 0)
            {
                visibleItems.Add($"{hiddenCount} more");
            }

            return string.Join(", ", visibleItems);
        }

        private static string FormatItem(string itemName, ItemActivity activity, bool includeGold, bool isSale)
        {
            if (!includeGold || activity.Gold == 0)
            {
                return $"{activity.Quantity}x {itemName}";
            }

            string sign = isSale ? "+" : "-";
            return $"{activity.Quantity}x {itemName} ({sign}{System.Math.Abs(activity.Gold)}d)";
        }
    }
}
