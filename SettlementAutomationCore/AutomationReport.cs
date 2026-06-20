using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace SettlementAutomationCore
{
    public enum AutomationTransactionStage
    {
        PreSell,
        PriorityRequest,
        SettlementCleanup,
        FreeTrade
    }

    public sealed class AutomationReportItem
    {
        public AutomationReportItem(string itemName, int quantity, int gold)
            : this(itemName, InventoryItemCategory.None, quantity, gold, 0)
        {
        }

        public AutomationReportItem(string itemName, InventoryItemCategory category, int quantity, int gold)
            : this(itemName, category, quantity, gold, 0)
        {
        }

        public AutomationReportItem(string itemName, InventoryItemCategory category, int quantity, int gold, int marketValue)
        {
            ItemName = itemName;
            Category = category;
            Quantity = quantity;
            Gold = gold;
            MarketValue = marketValue;
        }

        public string ItemName { get; }
        public InventoryItemCategory Category { get; }
        public string CategoryName => InventoryItemCategoryLabels.GetDisplayName(Category);
        public int Quantity { get; }
        public int Gold { get; }
        public int MarketValue { get; }
    }

    public sealed class AutomationProviderReport
    {
        public AutomationProviderReport(
            string providerName,
            AutomationTransactionStage stage,
            IReadOnlyList<AutomationReportItem> boughtItems,
            IReadOnlyList<AutomationReportItem> soldItems,
            IReadOnlyList<AutomationReportItem> slaughteredItems)
        {
            ProviderName = providerName;
            Stage = stage;
            BoughtItems = boughtItems;
            SoldItems = soldItems;
            SlaughteredItems = slaughteredItems;
        }

        public string ProviderName { get; }
        public AutomationTransactionStage Stage { get; }
        public IReadOnlyList<AutomationReportItem> BoughtItems { get; }
        public IReadOnlyList<AutomationReportItem> SoldItems { get; }
        public IReadOnlyList<AutomationReportItem> SlaughteredItems { get; }
    }

    public sealed class AutomationReportContext
    {
        public AutomationReportContext(
            Settlement settlement,
            string providerName,
            AutomationProviderReport report,
            string? cargoStatus)
        {
            Settlement = settlement;
            ProviderName = providerName;
            Report = report;
            CargoStatus = cargoStatus;
        }

        public Settlement Settlement { get; }
        public string ProviderName { get; }
        public AutomationTransactionStage Stage => Report.Stage;
        public AutomationProviderReport Report { get; }
        public IReadOnlyList<AutomationReportItem> BoughtItems => Report.BoughtItems;
        public IReadOnlyList<AutomationReportItem> SoldItems => Report.SoldItems;
        public IReadOnlyList<AutomationReportItem> SlaughteredItems => Report.SlaughteredItems;
        public string? CargoStatus { get; }
    }

    public interface IAutomationReportProvider
    {
        string ProviderName { get; }
        IReadOnlyList<string> BuildAutomationReportLines(AutomationReportContext context);
    }

    public interface IAutomationReportStyleProvider
    {
        uint? ReportHeaderColor { get; }
    }
}
