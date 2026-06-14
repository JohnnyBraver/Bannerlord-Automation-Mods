using System.Collections.Generic;
using System.Linq;

namespace SettlementAutomationCore
{
    internal sealed class MarketActivitySummary
    {
        private const int InGameItemLimit = 4;

        private readonly Dictionary<string, int> _boughtItems = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _soldItems = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _slaughteredItems = new Dictionary<string, int>();

        public int GoldDelta { get; private set; }

        public bool HasActivity => _boughtItems.Count > 0 || _soldItems.Count > 0 || _slaughteredItems.Count > 0;

        public void AddBought(string itemName, int quantity)
        {
            AddItem(_boughtItems, itemName, quantity);
        }

        public void AddSold(string itemName, int quantity)
        {
            AddItem(_soldItems, itemName, quantity);
        }

        public void AddSlaughtered(string itemName, int quantity)
        {
            AddItem(_slaughteredItems, itemName, quantity);
        }

        public void AddGoldDelta(int goldDelta)
        {
            GoldDelta += goldDelta;
        }

        public string BuildInGameSummary(string settlementName)
        {
            var parts = new List<string>();
            if (_boughtItems.Count > 0)
            {
                parts.Add($"bought {FormatItems(_boughtItems, InGameItemLimit)}");
            }
            if (_soldItems.Count > 0)
            {
                parts.Add($"sold {FormatItems(_soldItems, InGameItemLimit)}");
            }
            if (_slaughteredItems.Count > 0)
            {
                parts.Add($"slaughtered {FormatItems(_slaughteredItems, InGameItemLimit)}");
            }

            string goldSign = GoldDelta >= 0 ? "+" : "";
            return $"[Automation] Market at {settlementName}: {string.Join("; ", parts)}. Net {goldSign}{GoldDelta}d";
        }

        public string BuildLogSummary(string settlementName)
        {
            var parts = new List<string>();
            if (_boughtItems.Count > 0)
            {
                parts.Add($"Bought: {FormatItems(_boughtItems, int.MaxValue)}");
            }
            if (_soldItems.Count > 0)
            {
                parts.Add($"Sold: {FormatItems(_soldItems, int.MaxValue)}");
            }
            if (_slaughteredItems.Count > 0)
            {
                parts.Add($"Slaughtered: {FormatItems(_slaughteredItems, int.MaxValue)}");
            }

            string goldSign = GoldDelta >= 0 ? "+" : "";
            return $"Market automation summary at {settlementName} (Gold change: {goldSign}{GoldDelta}d). {string.Join(" ", parts)}";
        }

        private static void AddItem(Dictionary<string, int> items, string itemName, int quantity)
        {
            if (quantity <= 0 || string.IsNullOrWhiteSpace(itemName)) return;

            if (items.TryGetValue(itemName, out int current))
            {
                items[itemName] = current + quantity;
            }
            else
            {
                items.Add(itemName, quantity);
            }
        }

        private static string FormatItems(Dictionary<string, int> items, int maxItems)
        {
            var orderedItems = items
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key)
                .ToList();

            var visibleItems = orderedItems
                .Take(maxItems)
                .Select(item => $"{item.Value}x {item.Key}")
                .ToList();

            int hiddenCount = orderedItems.Count - visibleItems.Count;
            if (hiddenCount > 0)
            {
                visibleItems.Add($"{hiddenCount} more");
            }

            return string.Join(", ", visibleItems);
        }
    }
}

