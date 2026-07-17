using System;
using System.Collections.Generic;
using System.Linq;
using SettlementAutomationCore;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace SmithingOptimizer
{
    public class SmithingOptimizerProvider : IAutomationRequestProvider, IAutomationReportProvider, IAutomationReportStyleProvider
    {
        private const string HardwoodFallbackId = "hardwood";
        private const string CharcoalFallbackId = "charcoal";

        public string ProviderName => "SmithingOptimizer";
        public uint? ReportHeaderColor => 0xB0A89CFF;

        public void SubmitAutomationRequests(AutomationRequestContext context)
        {
            var settings = Settings.Instance;
            if (settings == null || context == null || !settings.AutoBuySmithingSupplies)
            {
                return;
            }

            // This request cannot be honored if the party starts at or below its own reserve.
            // Core remains authoritative for projected-gold validation after request ordering.
            if ((Hero.MainHero?.Gold ?? 0) <= settings.SupplyGoldReserve)
            {
                return;
            }

            SubmitSupplyRequest(context, "Hardwood", HardwoodFallbackId, settings.DesiredHardwood, settings);
            SubmitSupplyRequest(context, "Charcoal", CharcoalFallbackId, settings.DesiredCharcoal, settings);
        }

        public IReadOnlyList<string> BuildAutomationReportLines(AutomationReportContext context)
        {
            if (context == null ||
                context.Stage != AutomationTransactionStage.PriorityRequest ||
                context.BoughtItems.Count == 0)
            {
                return new List<string>();
            }

            string settlementName = context.Settlement?.Name?.ToString() ?? "Settlement";
            return new List<string>
            {
                $"[Smithing] Bought supplies @ {settlementName}: {FormatItems(context.BoughtItems)}"
            };
        }

        private static void SubmitSupplyRequest(
            AutomationRequestContext context,
            string displayName,
            string fallbackId,
            int desiredCount,
            Settings settings)
        {
            if (desiredCount <= 0)
            {
                return;
            }

            string targetId = ResolveItemId(context, fallbackId, displayName);
            AutomationRegistry.RegisterRequest(AutomationRequest.ForInventoryTarget(
                "SmithingOptimizer",
                RequestType.SpecificItem,
                targetId,
                desiredCount,
                settings.SupplyRequestProfile,
                settings.SupplyRequestPriority,
                BudgetPolicyKind.ExplicitReserve,
                settings.SupplyGoldReserve));
        }

        private static string ResolveItemId(AutomationRequestContext context, string fallbackId, string displayName)
        {
            var visibleItem = context.MerchantInventory.All
                .Concat(context.PlayerInventory.All)
                .Select(view => view.Item)
                .FirstOrDefault(item => MatchesItem(item, fallbackId, displayName));
            if (visibleItem != null && !string.IsNullOrWhiteSpace(visibleItem.StringId))
            {
                return visibleItem.StringId;
            }

            var objectManager = MBObjectManager.Instance;
            var knownItem = objectManager?.GetObjectTypeList<ItemObject>()
                .FirstOrDefault(item => MatchesItem(item, fallbackId, displayName));
            if (knownItem != null && !string.IsNullOrWhiteSpace(knownItem.StringId))
            {
                return knownItem.StringId;
            }

            return fallbackId;
        }

        private static bool MatchesItem(ItemObject item, string fallbackId, string displayName)
        {
            if (item == null)
            {
                return false;
            }

            if (string.Equals(item.StringId, fallbackId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string name = item.Name?.ToString() ?? string.Empty;
            return string.Equals(name, displayName, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatItems(IReadOnlyList<AutomationReportItem> items)
        {
            return string.Join(", ", items.Select(item =>
            {
                string gold = item.Gold != 0 ? $" (-{Math.Abs(item.Gold)}d)" : string.Empty;
                return $"{item.Quantity}x {item.ItemName}{gold}";
            }));
        }
    }
}
